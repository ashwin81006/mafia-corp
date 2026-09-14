package com.mafiacorp.server.networking;


import java.io.IOException;
import java.net.InetAddress;
import java.net.ServerSocket;
import java.net.Socket;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.atomic.AtomicInteger;

public class ConnectionManager {
    private final String host;
    private final int port;
    private RoomManager roomManager;
    private final AtomicInteger idGenerator = new AtomicInteger(1);
    private final ConcurrentHashMap<Integer, ClientConnection> activeConnections = new ConcurrentHashMap<>();

    public ConnectionManager(String host, int port) {
        this.host = host;
        this.port = port;
    }

    public void setRoomManager(RoomManager roomManager) {
        this.roomManager = roomManager;
    }

    public void startListening() {
        System.out.println("Starting Java GameServer on " + host + ":" + port);
        try (ServerSocket serverSocket = new ServerSocket(port, 50, InetAddress.getByName(host))) {
            while (true) {
                Socket socket = serverSocket.accept();
                int clientId = idGenerator.getAndIncrement();
                
                ClientConnection conn = new ClientConnection(socket, clientId, roomManager, this);
                activeConnections.put(clientId, conn);
                
                new Thread(conn).start();
            }
        } catch (IOException e) {
            e.printStackTrace();
        }
    }

    public void sendMessageTo(int clientId, Object msg) {
        ClientConnection conn = activeConnections.get(clientId);
        if (conn != null) {
            conn.sendMessage(msg);
        }
    }
    
    public void removeConnection(int clientId) {
        activeConnections.remove(clientId);
    }
}
