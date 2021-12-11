using Godot;
using System;

public struct PlayerPacket {
    public int clientID;
    public float timeSent;
    public Vector2 position;
    public float rotation;  
    public Vector2 velocity;   
}

public struct BulletPacket {
    public int clientID;
    public float timeSent;
    public Vector2 playerPosition;
    public float playerRotation;
    public float velocity;
}

// TODO

// remove the position queue and instead just send the most recent packet 
// do bullet prediction
// win screen -> either disconnect or restart game
// test game on different devices TBC