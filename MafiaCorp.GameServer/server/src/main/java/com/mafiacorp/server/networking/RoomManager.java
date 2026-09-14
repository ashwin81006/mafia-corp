package com.mafiacorp.server.networking;

import com.mafiacorp.server.game.GameManager;
import java.util.concurrent.ConcurrentHashMap;
import java.util.Random;

public class RoomManager {
    private final ConcurrentHashMap<String, GameManager> rooms = new ConcurrentHashMap<>();
    private final Random random = new Random();
    private final ConnectionManager connectionManager;

    public RoomManager(ConnectionManager connectionManager) {
        this.connectionManager = connectionManager;
    }

    public String createRoom() {
        String roomCode;
        do {
            roomCode = generateRoomCode();
        } while (rooms.containsKey(roomCode));

        final String finalRoomCode = roomCode;
        GameManager gameManager = new GameManager(
                finalRoomCode,
                connectionManager::sendMessageTo,
                (excludeId, msg) -> broadcastToRoom(finalRoomCode, excludeId, msg)
        );
        rooms.put(roomCode, gameManager);
        System.out.println("Room Created: " + roomCode);
        return roomCode;
    }

    public GameManager getRoom(String roomCode) {
        if (roomCode == null) return null;
        return rooms.get(roomCode.toUpperCase());
    }

    public void removeRoom(String roomCode) {
        rooms.remove(roomCode.toUpperCase());
        System.out.println("Room Removed: " + roomCode);
    }

    private void broadcastToRoom(String roomCode, int excludeId, Object msg) {
        GameManager gm = getRoom(roomCode);
        if (gm != null) {
            for (int clientId : gm.getPlayers().keySet()) {
                if (clientId != excludeId) {
                    connectionManager.sendMessageTo(clientId, msg);
                }
            }
        }
    }

    private String generateRoomCode() {
        String chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < 4; i++) {
            sb.append(chars.charAt(random.nextInt(chars.length())));
        }
        return sb.toString();
    }
}
