package com.mafiacorp.server.protocol;

import java.util.List;

public class Messages {

    // Base message for incoming JSON parsing
    public static class ClientMessage {
        public String type; // CREATE_ROOM, JOIN_ROOM, etc.
        public String roomCode; // For JOIN_ROOM
        public String userId;   
        public String username; 
        public String channel; 
        public String message; 
        public Integer targetPlayerId;
    }

    public static class RoomCreated {
        public String type = "ROOM_CREATED";
        public String roomCode;
        public RoomCreated(String roomCode) {
            this.roomCode = roomCode;
        }
    }

    // Outgoing Messages
    public static class RoomState {
        public String type = "ROOM_STATE";
        public String roomCode;
        public List<PlayerInfo> players;
        public int hostId;
        public String phase;

        public RoomState(String roomCode, List<PlayerInfo> players, int hostId, String phase) {
            this.roomCode = roomCode;
            this.players = players;
            this.hostId = hostId;
            this.phase = phase;
        }
    }

    public static class PlayerInfo {
        public int id;
        public String username;
        public boolean isAlive;
        public String role;

        public PlayerInfo(int id, String username, boolean isAlive) {
            this(id, username, isAlive, null);
        }

        public PlayerInfo(int id, String username, boolean isAlive, String role) {
            this.id = id;
            this.username = username;
            this.isAlive = isAlive;
            this.role = role;
        }
    }

    public static class PlayerJoined {
        public String type = "PLAYER_JOINED";
        public PlayerInfo player;
        public PlayerJoined(PlayerInfo player) { 
            this.player = player;
        }
    }

    public static class PlayerLeft {
        public String type = "PLAYER_LEFT";
        public int id;
        public PlayerLeft(int id) { this.id = id; }
    }

    public static class GameStarting {
        public String type = "GAME_STARTING";
    }

    public static class RoleAssigned {
        public String type = "ROLE_ASSIGNED";
        public String role;
        public List<Integer> mafiaTeammates; // Optional, sent only to Mafia
        public List<String> mafiaPartners;

        public RoleAssigned(String role, List<Integer> mafiaTeammates) {
            this(role, mafiaTeammates, null);
        }

        public RoleAssigned(String role, List<Integer> mafiaTeammates, List<String> mafiaPartners) {
            this.role = role;
            this.mafiaTeammates = mafiaTeammates;
            this.mafiaPartners = mafiaPartners;
        }
    }

    public static class PhaseChanged {
        public String type = "PHASE_CHANGED";
        public String phase;
        public int durationSeconds;
        public int round = 1;
        
        public PhaseChanged(String phase, int durationSeconds) {
            this(phase, durationSeconds, 1);
        }

        public PhaseChanged(String phase, int durationSeconds, int round) {
            this.phase = phase;
            this.durationSeconds = durationSeconds;
            this.round = round;
        }
    }

    public static class ChatMessage {
        public String type; // PUBLIC_CHAT_MESSAGE or MAFIA_CHAT_MESSAGE
        public int senderId;
        public String message;

        public ChatMessage(String type, int senderId, String message) {
            this.type = type;
            this.senderId = senderId;
            this.message = message;
        }
    }

    public static class InvestigationResult {
        public String type = "INVESTIGATION_RESULT";
        public String result; // SUSPICIOUS or NOT_SUSPICIOUS
        public int targetId;

        public InvestigationResult(int targetId, String result) {
            this.targetId = targetId;
            this.result = result;
        }
    }

    public static class DoctorSaved {
        public String type = "DOCTOR_SAVED";
        public int targetId;
        public DoctorSaved(int targetId) {
            this.targetId = targetId;
        }
    }

    public static class PlayerDied {
        public String type = "PLAYER_DIED";
        public int id;
        public String username;
        public String role;

        public PlayerDied(int id, String username) {
            this(id, username, null);
        }

        public PlayerDied(int id, String username, String role) {
            this.id = id;
            this.username = username;
            this.role = role;
        }
    }
    
    public static class VotingStarted {
        public String type = "VOTING_STARTED";
    }

    public static class VoteUpdate {
        public String type = "VOTE_UPDATE";
        public int voterId;
        public int targetId;
        public VoteUpdate(int voterId, int targetId) {
            this.voterId = voterId;
            this.targetId = targetId;
        }
    }

    public static class PlayerEliminated {
        public String type = "PLAYER_ELIMINATED";
        public int id;
        public String username;
        public String role;

        public PlayerEliminated(int id, String username) {
            this(id, username, null);
        }

        public PlayerEliminated(int id, String username, String role) {
            this.id = id;
            this.username = username;
            this.role = role;
        }
    }

    public static class GameOver {
        public String type = "GAME_OVER";
        public String winner; // MAFIA or CITIZENS
        public GameOver(String winner) {
            this.winner = winner;
        }
    }

    public static class CandidateVote {
        public String candidate;
        public int votes;
        public List<String> voters;

        public CandidateVote(String candidate, int votes) {
            this(candidate, votes, null);
        }

        public CandidateVote(String candidate, int votes, List<String> voters) {
            this.candidate = candidate;
            this.votes = votes;
            this.voters = voters;
        }
    }

    public static class VotingResults {
        public String type = "VOTING_RESULTS";
        public List<CandidateVote> candidateVotes;
        public String eliminatedUsername;
        public String eliminatedRole;
        public boolean isTie;

        public VotingResults(List<CandidateVote> candidateVotes, String eliminatedUsername, String eliminatedRole, boolean isTie) {
            this.candidateVotes = candidateVotes;
            this.eliminatedUsername = eliminatedUsername;
            this.eliminatedRole = eliminatedRole;
            this.isTie = isTie;
        }
    }
}
