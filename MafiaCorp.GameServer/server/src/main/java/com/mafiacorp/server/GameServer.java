package com.mafiacorp.server;

import com.mafiacorp.server.networking.ConnectionManager;

public class GameServer {
    public static void main(String[] args) {
        String host = System.getenv("HOST");
        if (host == null || host.isEmpty()) {
            host = "0.0.0.0";
        }
        
        String portStr = System.getenv("PORT");
        int port = 54555;
        if (portStr != null && !portStr.isEmpty()) {
            try {
                port = Integer.parseInt(portStr);
            } catch (NumberFormatException e) {
                System.out.println("Invalid PORT, defaulting to 54555");
            }
        }
        
        ConnectionManager connectionManager = new ConnectionManager(host, port);
        com.mafiacorp.server.networking.RoomManager roomManager = new com.mafiacorp.server.networking.RoomManager(connectionManager);
        connectionManager.setRoomManager(roomManager);
        connectionManager.startListening();
    }
}
