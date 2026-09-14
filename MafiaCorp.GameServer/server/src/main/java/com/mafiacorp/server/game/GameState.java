package com.mafiacorp.server.game;

public enum GameState {
    WAITING_FOR_PLAYERS,
    STARTING,
    ROLE_ASSIGNMENT,
    NIGHT,
    NIGHT_RESOLUTION,
    DAY,
    VOTING,
    VOTE_RESOLUTION,
    GAME_OVER
}
