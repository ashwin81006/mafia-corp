package com.mafiacorp.server.networking;

import com.google.gson.Gson;
import com.mafiacorp.server.game.GameManager;
import com.mafiacorp.server.protocol.Messages.ClientMessage;
import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStreamReader;
import java.io.PrintWriter;
import java.net.Socket;

public class ClientConnection implements Runnable {
    private final Socket socket;
    private final int clientId;
    private final ConnectionManager connectionManager;
    private final RoomManager roomManager;
    private final Gson gson = new Gson();
    
    private PrintWriter out;
    private BufferedReader in;
    private String currentRoomCode = null;

    public ClientConnection(Socket socket, int clientId, RoomManager roomManager, ConnectionManager connectionManager) {
        this.socket = socket;
        this.clientId = clientId;
        this.roomManager = roomManager;
        this.connectionManager = connectionManager;
    }

    @Override
    public void run() {
        try {
            System.out.println("Client connected: " + clientId);
            out = new PrintWriter(socket.getOutputStream(), true);
            in = new BufferedReader(new InputStreamReader(socket.getInputStream()));

            String line;
            while ((line = in.readLine()) != null) {
                ClientMessage msg = gson.fromJson(line, ClientMessage.class);
                
                if ("CREATE_ROOM".equals(msg.type)) {
                    currentRoomCode = roomManager.createRoom();
                    sendMessage(new com.mafiacorp.server.protocol.Messages.RoomCreated(currentRoomCode));
                    continue;
                }
                
                if ("JOIN_ROOM".equals(msg.type)) {
                    if (msg.roomCode == null || roomManager.getRoom(msg.roomCode) == null) {
                        System.out.println("Invalid room code: " + msg.roomCode);
                        continue;
                    }
                    currentRoomCode = msg.roomCode.toUpperCase();
                }

                if (currentRoomCode != null) {
                    GameManager gm = roomManager.getRoom(currentRoomCode);
                    if (gm != null) {
                        gm.handleClientMessage(clientId, msg);
                    }
                }
            }
        } catch (IOException e) {
            System.out.println("Client disconnected unexpectedly: " + clientId);
        } finally {
            System.out.println("Client removed: " + clientId);
            if (currentRoomCode != null) {
                GameManager gm = roomManager.getRoom(currentRoomCode);
                if (gm != null) {
                    gm.removePlayer(clientId);
                    if (gm.getPlayers().isEmpty()) {
                        roomManager.removeRoom(currentRoomCode);
                    }
                }
            }
            connectionManager.removeConnection(clientId);
            try { socket.close(); } catch (IOException ignored) {}
        }
    }

    public void sendMessage(Object msg) {
        if (out != null) {
            out.println(gson.toJson(msg));
        }
    }
}
