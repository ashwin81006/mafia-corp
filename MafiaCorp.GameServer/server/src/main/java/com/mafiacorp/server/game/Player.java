package com.mafiacorp.server.game;

public class Player {
    private final int id;
    private String username;
    private Role role;
    private boolean isAlive;
    private boolean isHost;

    private String userId;
    private int kills;

    public Player(int id) {
        this.id = id;
        this.userId = null;
        this.kills = 0;
        this.role = Role.UNASSIGNED;
        this.isAlive = true;
        this.isHost = false;
    }

    public int getId() {
        return id;
    }

    public String getUsername() {
        return username;
    }

    public void setUsername(String username) {
        this.username = username;
    }

    public Role getRole() {
        return role;
    }

    public void setRole(Role role) {
        this.role = role;
    }

    public boolean isAlive() {
        return isAlive;
    }

    public void setAlive(boolean isAlive) {
        this.isAlive = isAlive;
    }

    public boolean isHost() {
        return isHost;
    }

    public void setHost(boolean isHost) {
        this.isHost = isHost;
    }

    public String getUserId() {
        return userId;
    }

    public void setUserId(String userId) {
        this.userId = userId;
    }

    public int getKills() {
        return kills;
    }

    public void incrementKills() {
        this.kills++;
    }
}
