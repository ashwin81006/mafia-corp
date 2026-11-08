package com.mafiacorp.server.networking;

import com.google.gson.Gson;
import com.mafiacorp.server.game.GameManager;
import com.mafiacorp.server.protocol.Messages.ClientMessage;
import com.mafiacorp.server.protocol.Messages.RoomCreated;
import java.net.InetSocketAddress;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.atomic.AtomicInteger;
import org.java_websocket.WebSocket;
import org.java_websocket.handshake.ClientHandshake;
import org.java_websocket.server.WebSocketServer;

public class ConnectionManager extends WebSocketServer {
    private final AtomicInteger idGenerator = new AtomicInteger(1);
    private final ConcurrentHashMap<Integer, WebSocket> activeConnections = new ConcurrentHashMap<>();
    private final ConcurrentHashMap<Integer, String> clientRooms = new ConcurrentHashMap<>();
    private final Gson gson = new Gson();
    private RoomManager roomManager;

    public ConnectionManager(String host, int port) {
        super(new InetSocketAddress(host, port));
    }

    public void setRoomManager(RoomManager roomManager) {
        this.roomManager = roomManager;
    }

    public void startListening() {
        setReuseAddr(true);
        start();
        System.out.println("WebSocket GameServer starting on " + getAddress());
    }

    @Override
    public void onStart() {
        System.out.println("WebSocket GameServer is listening on " + getAddress());
    }

    @Override
    public void onOpen(WebSocket conn, ClientHandshake handshake) {
        int clientId = idGenerator.getAndIncrement();
        conn.setAttachment(clientId);
        activeConnections.put(clientId, conn);
        System.out.println("WebSocket Client connected: ID #" + clientId + " from " + conn.getRemoteSocketAddress());
    }

    @Override
    public void onMessage(WebSocket conn, String message) {
        Integer clientId = conn.getAttachment();
        if (clientId == null || message == null || message.trim().isEmpty()) return;

        try {
            ClientMessage msg = gson.fromJson(message, ClientMessage.class);
            if (msg == null) return;

            if ("CREATE_ROOM".equals(msg.type)) {
                String roomCode = roomManager.createRoom();
                clientRooms.put(clientId, roomCode);
                sendMessageTo(clientId, new RoomCreated(roomCode));
                return;
            }

            if ("JOIN_ROOM".equals(msg.type)) {
                if (msg.roomCode == null || roomManager.getRoom(msg.roomCode) == null) {
                    System.out.println("Invalid room code: " + msg.roomCode + " from Client #" + clientId);
                    return;
                }
                clientRooms.put(clientId, msg.roomCode.toUpperCase());
            }

            String currentRoomCode = clientRooms.get(clientId);
            if (currentRoomCode != null) {
                GameManager gm = roomManager.getRoom(currentRoomCode);
                if (gm != null) {
                    gm.handleClientMessage(clientId, msg);
                }
            }
        } catch (Exception e) {
            System.err.println("Error processing message from Client #" + clientId + ": " + e.getMessage());
        }
    }

    @Override
    public void onClose(WebSocket conn, int code, String reason, boolean remote) {
        Integer clientId = conn.getAttachment();
        if (clientId != null) {
            activeConnections.remove(clientId);
            String roomCode = clientRooms.remove(clientId);
            if (roomCode != null) {
                GameManager gm = roomManager.getRoom(roomCode);
                if (gm != null) {
                    gm.removePlayer(clientId);
                    if (gm.getPlayers().isEmpty()) {
                        roomManager.removeRoom(roomCode);
                    }
                }
            }
            System.out.println("WebSocket Client disconnected: ID #" + clientId + " (code: " + code + ", reason: " + reason + ")");
        }
    }

    @Override
    public void onError(WebSocket conn, Exception ex) {
        Integer clientId = conn != null ? (Integer) conn.getAttachment() : null;
        System.err.println("WebSocket error on Client #" + (clientId != null ? clientId : "unknown") + ": " + (ex != null ? ex.getMessage() : "null"));
    }

    public void sendMessageTo(int clientId, Object msg) {
        WebSocket conn = activeConnections.get(clientId);
        if (conn != null && conn.isOpen()) {
            try {
                conn.send(gson.toJson(msg));
            } catch (Exception e) {
                System.err.println("Failed to send message to Client #" + clientId + ": " + e.getMessage());
            }
        }
    }

    public void removeConnection(int clientId) {
        WebSocket conn = activeConnections.remove(clientId);
        clientRooms.remove(clientId);
        if (conn != null && conn.isOpen()) {
            conn.close();
        }
    }
}
