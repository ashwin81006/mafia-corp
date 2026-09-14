package com.mafiacorp.server.game;

import com.mafiacorp.server.protocol.Messages.*;
import java.util.*;
import java.util.concurrent.*;
import java.util.function.BiConsumer;
import java.util.stream.Collectors;
import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.time.Instant;
import com.google.gson.Gson;

public class GameManager {
    private final String roomCode;
    private final ConcurrentHashMap<Integer, Player> players = new ConcurrentHashMap<>();
    private final BiConsumer<Integer, Object> sendToClient;
    private final BiConsumer<Integer, Object> broadcastToOthers;

    private GameState currentState = GameState.WAITING_FOR_PLAYERS;
    private int hostId = -1;
    private Instant gameStartTime;

    public ConcurrentHashMap<Integer, Player> getPlayers() {
        return players;
    }

    private final ScheduledExecutorService scheduler = Executors.newScheduledThreadPool(1);
    private ScheduledFuture<?> phaseTask;

    private int roundNumber = 1;

    // Night Actions
    private Integer mafiaTargetId = null;
    private Integer doctorTargetId = null;
    private boolean detectiveActed = false;

    // Voting
    private final Map<Integer, Integer> votes = new ConcurrentHashMap<>();

    public GameManager(String roomCode, BiConsumer<Integer, Object> sendToClient, BiConsumer<Integer, Object> broadcastToOthers) {
        this.roomCode = roomCode;
        this.sendToClient = sendToClient;
        this.broadcastToOthers = broadcastToOthers;
    }

    public synchronized void handleClientMessage(int clientId, ClientMessage msg) {
        if ("JOIN_ROOM".equals(msg.type)) {
            handleJoinRoom(clientId, msg.username, msg.userId);
        } else if ("START_GAME".equals(msg.type)) {
            handleStartGame(clientId);
        } else if ("PUBLIC_CHAT_MESSAGE".equals(msg.type) || "CHAT_MESSAGE".equals(msg.type)) {
            handlePublicChat(clientId, msg.message);
        } else if ("MAFIA_CHAT_MESSAGE".equals(msg.type)) {
            handleMafiaChat(clientId, msg.message);
        } else if ("NIGHT_ACTION".equals(msg.type)) {
            handleNightAction(clientId, msg.targetPlayerId, msg.message); // message can distinguish Kill/Protect/Investigate
        } else if ("CAST_VOTE".equals(msg.type) || "VOTE".equals(msg.type)) {
            handleCastVote(clientId, msg.targetPlayerId);
        }
    }

    private void handleJoinRoom(int clientId, String username, String userId) {
        if (currentState != GameState.WAITING_FOR_PLAYERS) {
            // Can't join once started
            return;
        }
        Player newPlayer = new Player(clientId);
        newPlayer.setUsername(username);
        newPlayer.setUserId(userId);
        
        if (players.isEmpty()) {
            newPlayer.setHost(true);
            hostId = clientId;
        }
        
        players.put(clientId, newPlayer);

        // Tell the new client the room state
        sendMessageTo(clientId, new RoomState(roomCode, getPlayerInfos(), hostId, currentState.name()));

        // Broadcast to others
        broadcastToOthers.accept(clientId, new PlayerJoined(new PlayerInfo(clientId, username, true)));
    }

    public synchronized void removePlayer(int clientId) {
        players.remove(clientId);
        broadcastAll(new PlayerLeft(clientId));

        if (clientId == hostId && !players.isEmpty()) {
            // Reassign host
            hostId = players.keySet().iterator().next();
            players.get(hostId).setHost(true);
            // Broadcast room state again to update host
            broadcastAll(new RoomState(roomCode, getPlayerInfos(), hostId, currentState.name()));
        }

        if (players.isEmpty()) {
            resetMatch();
        }
    }

    private List<PlayerInfo> getPlayerInfos() {
        return players.values().stream()
                .map(p -> new PlayerInfo(
                        p.getId(),
                        p.getUsername(),
                        p.isAlive(),
                        !p.isAlive() && p.getRole() != null ? p.getRole().name() : null
                ))
                .collect(Collectors.toList());
    }

    private void handleStartGame(int clientId) {
        if (clientId != hostId || currentState != GameState.WAITING_FOR_PLAYERS) return;
        
        if (players.size() < 6) {
            // Need at least 6 for the requested configuration
            System.out.println("Cannot start: Need 6 players, have " + players.size());
            return;
        }

        currentState = GameState.STARTING;
        roundNumber = 1;
        gameStartTime = Instant.now();
        broadcastAll(new GameStarting());

        // Broadcast current ROOM_STATE so clients entering GamePage have full roster
        broadcastAll(new RoomState(roomCode, getPlayerInfos(), hostId, currentState.name()));

        // Assign Roles
        List<Player> playerList = new ArrayList<>(players.values());
        Collections.shuffle(playerList);

        // 1 Mafia, 1 Doctor, 1 Detective, 3 Citizens
        playerList.get(0).setRole(Role.MAFIA);
        playerList.get(1).setRole(Role.DOCTOR);
        playerList.get(2).setRole(Role.DETECTIVE);
        for (int i = 3; i < playerList.size(); i++) {
            playerList.get(i).setRole(Role.CITIZEN);
        }

        List<Integer> mafiaIds = playerList.stream()
                .filter(p -> p.getRole() == Role.MAFIA)
                .map(Player::getId)
                .collect(Collectors.toList());

        List<String> mafiaPartners = playerList.stream()
                .filter(p -> p.getRole() == Role.MAFIA)
                .map(Player::getUsername)
                .collect(Collectors.toList());

        for (Player p : playerList) {
            if (p.getRole() == Role.MAFIA) {
                sendMessageTo(p.getId(), new RoleAssigned(p.getRole().name(), mafiaIds, mafiaPartners));
            } else {
                sendMessageTo(p.getId(), new RoleAssigned(p.getRole().name(), null, null));
            }
        }

        // Give clients 9 seconds for the full-screen role reveal overlay (8s countdown) before Night 1 begins
        scheduler.schedule(this::transitionToNight, 9, TimeUnit.SECONDS);
    }

    private void transitionToNight() {
        currentState = GameState.NIGHT;
        mafiaTargetId = null;
        doctorTargetId = null;
        detectiveActed = false;

        broadcastAll(new PhaseChanged(currentState.name(), 30, roundNumber));

        schedulePhaseTransition(this::resolveNight, 30);
    }

    private synchronized void handleNightAction(int clientId, Integer targetId, String actionType) {
        if (currentState != GameState.NIGHT) return;
        Player p = players.get(clientId);
        if (p == null || !p.isAlive()) return;
        
        Player target = players.get(targetId);
        if (target == null || !target.isAlive()) return;

        // Mafia cannot kill themselves, Detective cannot investigate themselves; Doctor CAN self-protect
        if (clientId == targetId && p.getRole() != Role.DOCTOR) {
            System.out.println("Player " + clientId + " tried to self-target with role " + p.getRole() + ", rejected.");
            return;
        }

        if (p.getRole() == Role.MAFIA && ("KILL".equalsIgnoreCase(actionType) || actionType == null || actionType.isEmpty())) {
            mafiaTargetId = targetId;
        } else if (p.getRole() == Role.DOCTOR && ("PROTECT".equalsIgnoreCase(actionType) || "HEAL".equalsIgnoreCase(actionType))) {
            doctorTargetId = targetId;
        } else if (p.getRole() == Role.DETECTIVE && ("INVESTIGATE".equalsIgnoreCase(actionType) || actionType == null || actionType.isEmpty())) {
            if (!detectiveActed) {
                detectiveActed = true;
                String result = (target.getRole() == Role.MAFIA) ? "SUSPICIOUS" : "NOT_SUSPICIOUS";
                sendMessageTo(clientId, new InvestigationResult(targetId, result));
            }
        }
    }

    private synchronized void resolveNight() {
        if (currentState != GameState.NIGHT) return;
        currentState = GameState.NIGHT_RESOLUTION;
        
        Integer victimId = null;
        String victimName = null;
        String victimRole = null;
        boolean doctorSaved = false;

        if (mafiaTargetId != null) {
            if (doctorTargetId != null && doctorTargetId.equals(mafiaTargetId)) {
                // Doctor successfully protected the target!
                doctorSaved = true;
                victimId = mafiaTargetId;
            } else {
                Player victim = players.get(mafiaTargetId);
                if (victim != null && victim.isAlive()) {
                    victim.setAlive(false);
                    victimId = mafiaTargetId;
                    victimName = victim.getUsername();
                    victimRole = victim.getRole() != null ? victim.getRole().name() : "CITIZEN";
                    for (Player p : players.values()) {
                        if (p.isAlive() && p.getRole() == Role.MAFIA) {
                            p.incrementKills();
                        }
                    }
                }
            }
        }

        if (checkWinCondition()) {
            if (victimId != null && victimName != null) {
                broadcastAll(new PlayerDied(victimId, victimName, victimRole));
            }
            return;
        }

        transitionToDay(victimId, victimName, victimRole, doctorSaved);
    }

    private void transitionToDay(Integer victimId, String victimName, String victimRole, boolean doctorSaved) {
        currentState = GameState.DAY;
        // 1. Broadcast Day PhaseChanged FIRST so dawn announcement appears before casualty report
        broadcastAll(new PhaseChanged(currentState.name(), 45, roundNumber));

        // 2. Broadcast night casualties or doctor save SECOND
        if (doctorSaved && victimId != null) {
            broadcastAll(new DoctorSaved(victimId));
        } else if (victimId != null && victimName != null) {
            broadcastAll(new PlayerDied(victimId, victimName, victimRole));
        }

        schedulePhaseTransition(this::transitionToVoting, 45);
    }

    private synchronized void transitionToVoting() {
        if (currentState != GameState.DAY) return;
        currentState = GameState.VOTING;
        votes.clear();
        broadcastAll(new VotingStarted());
        broadcastAll(new PhaseChanged(currentState.name(), 30, roundNumber));
        schedulePhaseTransition(this::resolveVoting, 30);
    }

    private synchronized void handleCastVote(int clientId, Integer targetId) {
        if (currentState != GameState.VOTING) return;
        Player voter = players.get(clientId);
        Player target = players.get(targetId);
        
        if (voter == null || !voter.isAlive()) return;
        if (target == null || !target.isAlive()) return;

        votes.put(clientId, targetId);
        broadcastAll(new VoteUpdate(clientId, targetId));
    }

    private synchronized void resolveVoting() {
        if (currentState != GameState.VOTING) return;
        currentState = GameState.VOTE_RESOLUTION;

        // Tally votes and track voter names for each candidate
        Map<Integer, Integer> voteCounts = new HashMap<>();
        Map<Integer, List<String>> candidateVoters = new HashMap<>();

        for (Map.Entry<Integer, Integer> voteEntry : votes.entrySet()) {
            int voterId = voteEntry.getKey();
            int targetId = voteEntry.getValue();

            voteCounts.put(targetId, voteCounts.getOrDefault(targetId, 0) + 1);

            Player voter = players.get(voterId);
            String voterName = voter != null ? voter.getUsername() : ("Player #" + voterId);
            candidateVoters.computeIfAbsent(targetId, k -> new ArrayList<>()).add(voterName);
        }

        // Find max votes
        int maxVotes = 0;
        Integer eliminatedId = null;
        boolean tie = false;

        for (Map.Entry<Integer, Integer> entry : voteCounts.entrySet()) {
            if (entry.getValue() > maxVotes) {
                maxVotes = entry.getValue();
                eliminatedId = entry.getKey();
                tie = false;
            } else if (entry.getValue() == maxVotes) {
                tie = true;
            }
        }

        // Build candidate votes list (only candidates who received votes)
        List<CandidateVote> candidateVotes = new ArrayList<>();
        for (Map.Entry<Integer, Integer> entry : voteCounts.entrySet()) {
            Player target = players.get(entry.getKey());
            if (target != null && entry.getValue() > 0) {
                List<String> votersList = candidateVoters.getOrDefault(entry.getKey(), Collections.emptyList());
                candidateVotes.add(new CandidateVote(target.getUsername(), entry.getValue(), votersList));
            }
        }
        candidateVotes.sort((a, b) -> Integer.compare(b.votes, a.votes));

        String eliminatedName = null;
        String eliminatedRole = null;
        if (eliminatedId != null && !tie) {
            Player eliminated = players.get(eliminatedId);
            if (eliminated != null && eliminated.isAlive()) {
                eliminated.setAlive(false);
                eliminatedName = eliminated.getUsername();
                eliminatedRole = eliminated.getRole() != null ? eliminated.getRole().name() : "CITIZEN";
            }
        } else {
            // No one eliminated on tie
            System.out.println("Voting tied or no votes, no one eliminated.");
        }

        // 1. Broadcast detailed voting results FIRST
        broadcastAll(new VotingResults(candidateVotes, eliminatedName, eliminatedRole, tie));

        // 2. Broadcast elimination verdict SECOND (if someone was eliminated)
        if (eliminatedId != null && !tie && eliminatedName != null) {
            broadcastAll(new PlayerEliminated(eliminatedId, eliminatedName, eliminatedRole));
        }

        if (!checkWinCondition()) {
            roundNumber++;
            // 5-second pause before next night so players have ample time to read the voting results
            scheduler.schedule(this::transitionToNight, 5, TimeUnit.SECONDS);
        }
    }

    private boolean checkWinCondition() {
        long aliveMafia = players.values().stream().filter(p -> p.isAlive() && p.getRole() == Role.MAFIA).count();
        long aliveNonMafia = players.values().stream().filter(p -> p.isAlive() && p.getRole() != Role.MAFIA).count();

        if (aliveMafia == 0) {
            endGame("CITIZENS");
            return true;
        } else if (aliveMafia >= aliveNonMafia) {
            endGame("MAFIA");
            return true;
        }
        return false;
    }

    private void endGame(String winner) {
        currentState = GameState.GAME_OVER;
        broadcastAll(new GameOver(winner));
        
        sendMatchResult(winner);
        
        if (phaseTask != null) {
            phaseTask.cancel(false);
        }
        
        // Reset match after some time so players can review results and play again
        scheduler.schedule(this::resetMatch, 60, TimeUnit.SECONDS);
    }

    private void sendMatchResult(String winner) {
        try {
            Map<String, Object> payload = new HashMap<>();
            payload.put("winningFaction", winner);
            payload.put("startedAt", gameStartTime != null ? gameStartTime.toString() : Instant.now().toString());
            payload.put("endedAt", Instant.now().toString());

            List<Map<String, Object>> playersData = new ArrayList<>();
            for (Player p : players.values()) {
                if (p.getUserId() == null || p.getUserId().isEmpty()) continue; // Skip guests
                
                Map<String, Object> pData = new HashMap<>();
                pData.put("userId", p.getUserId());
                pData.put("role", p.getRole().name());
                boolean won = (winner.equals("MAFIA") && p.getRole() == Role.MAFIA) || (winner.equals("CITIZENS") && p.getRole() != Role.MAFIA);
                pData.put("won", won);
                pData.put("survived", p.isAlive());
                pData.put("kills", p.getKills());
                pData.put("votesReceived", 0);
                playersData.add(pData);
            }
            payload.put("players", playersData);

            String json = new Gson().toJson(payload);
            System.out.println("Sending Match Result: " + json);

            String apiBase = System.getenv().getOrDefault("API_URL", "http://localhost:5130");
            HttpClient client = HttpClient.newHttpClient();
            HttpRequest request = HttpRequest.newBuilder()
                    .uri(URI.create(apiBase + "/api/internal/matches"))
                    .header("Content-Type", "application/json")
                    .header("X-Internal-Secret", "SuperSecretKeyForInternalJavaServer123!")
                    .POST(HttpRequest.BodyPublishers.ofString(json))
                    .build();

            client.sendAsync(request, HttpResponse.BodyHandlers.ofString())
                  .thenAccept(res -> System.out.println("API Response: " + res.statusCode()));
        } catch (Exception e) {
            System.err.println("Failed to send match results: " + e.getMessage());
        }
    }

    private synchronized void resetMatch() {
        currentState = GameState.WAITING_FOR_PLAYERS;
        roundNumber = 1;
        for (Player p : players.values()) {
            p.setRole(Role.UNASSIGNED);
            p.setAlive(true);
        }
        if (phaseTask != null) {
            phaseTask.cancel(false);
        }
        
        broadcastAll(new RoomState(roomCode, getPlayerInfos(), hostId, currentState.name()));
    }

    private void handlePublicChat(int clientId, String message) {
        if (currentState != GameState.DAY && currentState != GameState.VOTING && currentState != GameState.WAITING_FOR_PLAYERS) return;
        Player p = players.get(clientId);
        if (p == null || (!p.isAlive() && currentState != GameState.WAITING_FOR_PLAYERS)) return;

        broadcastAll(new ChatMessage("PUBLIC_CHAT_MESSAGE", clientId, message));
    }

    private void handleMafiaChat(int clientId, String message) {
        if (currentState != GameState.NIGHT) return;
        Player p = players.get(clientId);
        if (p == null || !p.isAlive() || p.getRole() != Role.MAFIA) return;

        ChatMessage chatMsg = new ChatMessage("MAFIA_CHAT_MESSAGE", clientId, message);
        for (Player other : players.values()) {
            if (other.isAlive() && other.getRole() == Role.MAFIA) {
                sendMessageTo(other.getId(), chatMsg);
            }
        }
    }

    private void schedulePhaseTransition(Runnable task, int delaySeconds) {
        if (phaseTask != null) {
            phaseTask.cancel(false);
        }
        phaseTask = scheduler.schedule(() -> {
            try {
                task.run();
            } catch (Exception e) {
                e.printStackTrace();
            }
        }, delaySeconds, TimeUnit.SECONDS);
    }

    public void sendMessageTo(int clientId, Object msg) {
        sendToClient.accept(clientId, msg);
    }
    
    public void broadcastAll(Object msg) {
        broadcastToOthers.accept(-1, msg);
    }
}
