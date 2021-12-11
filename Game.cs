using Godot;
using System;

public class Game : Node
{
    struct PlayerTransforms{
        public Vector2 newPosition; // new position recieved from packet
        public Vector2 predictedPos;
        public Vector2 oldPosition; // other player's current position at time of recieving new packet
        public float newRotation; // new rotationion recieved from packet
        public float oldRotation; // other player's current rotation at time of recieving new packet
        public float lerpTime; // the player's current 't' value in the lerp function
        public Vector2 velocity;
    }
    
    Network network;    
    MenuManager menu;
    Camera2D camera;

    public bool playing;
    public Player[] player = new Player[4];
    PlayerTransforms[] pTransforms = new PlayerTransforms[4]; // stores each player's transform information
    float packetSendTimer = 0;
    float lerpSeconds = 0.075f; // how long it takes for a player to lerp from an old position to new position
    int clientNumber;
    public int playersDead = 0;    

    public override void _Ready()
    {
        network  = GetNode<Network>("../Network"); 
        menu = GetNode<MenuManager>("../MenuManager");
        camera = GetNode<Camera2D>("../Camera2D");
        player[0] = GetNode<Player>("../Player0");
        player[1] = GetNode<Player>("../Player1");
        player[2] = GetNode<Player>("../Player2");
        player[3] = GetNode<Player>("../Player3");

        for(int i = 0; i < 4;i++){
            // reset all the position data
            pTransforms[i].oldPosition = player[i].Position;
            pTransforms[i].newPosition = player[i].Position;
            player[i].playerNumber = i;
        }
    }

    public override void _Process(float _delta){
        if(playing && !network.isServer){
            // every 0.1s send position data to server
            
            camera.Position = player[clientNumber].Position;

            menu.healthBar.Value = player[clientNumber].health;

            if(!player[clientNumber].isDead)
                if(packetSendTimer > 0.03f){
                    sendTransformPacket();
                    packetSendTimer = 0.0f;
                }else{
                    packetSendTimer += _delta;
                }

            // apply predicted positions to other players
            for(int i = 0; i < 4; i++){
                if(i != clientNumber){
                    // how long it will take to get from the old position to the new position
                    pTransforms[i].lerpTime += _delta / lerpSeconds;
                    // get the predicted position
                    pTransforms[i].predictedPos = pTransforms[i].newPosition + pTransforms[i].velocity * _delta;
                    // get the current position position
                    Vector2 pos = new Vector2(
                        Mathf.Lerp(pTransforms[i].oldPosition.x, pTransforms[i].predictedPos.x, pTransforms[i].lerpTime),
                        Mathf.Lerp(pTransforms[i].oldPosition.y, pTransforms[i].predictedPos.y, pTransforms[i].lerpTime));

                    player[i].Position = pos;
                    // get the current rotation                    
                    player[i].Rotation = Mathf.LerpAngle(pTransforms[i].oldRotation, pTransforms[i].newRotation, pTransforms[i].lerpTime);
                }
            }
        }
    }

    public void begin(int _playerNum){
        // set each player's texture
        for(int i = 0; i < 4; i++){
            player[i].setTexture(i);        
        }

        clientNumber = _playerNum;

        playing = true;
    }

    // send a message to the server saying that this player has been hit
    public void sendHit(int _playerHit){
        network.send(Network.hostPort, network.serverIp, _playerHit, Serialiser.PacketType.BulletHit, true);
    }

    // sends a packet to the server containing a player's most recent position data
    void sendTransformPacket(){
        PlayerPacket packet;
        packet.clientID = clientNumber;
        packet.timeSent = menu.time;
        packet.position.x = player[clientNumber].Position.x;
        packet.position.y = player[clientNumber].Position.y;
        packet.rotation = player[clientNumber].Rotation;
        packet.velocity = player[clientNumber].velocity;
        network.send(Network.hostPort, network.serverIp, (PlayerPacket)packet, Serialiser.PacketType.PlayerTransform, false);
    }

    // sends a bullet packet to the server containing the bullets position and rotation
    public void sendBullet(){
        BulletPacket packet;
        packet.clientID = clientNumber;
        packet.timeSent = menu.time;
        packet.playerPosition.x = player[clientNumber].weapon.GlobalPosition.x;
        packet.playerPosition.y = player[clientNumber].weapon.GlobalPosition.y;
        packet.playerRotation = player[clientNumber].Rotation;
        packet.velocity = Bullet.bulletSpeed;

        network.send(Network.hostPort, network.serverIp, (BulletPacket)packet, Serialiser.PacketType.Bullet, false);        
    }

    // used by the serialiser to spawn a bullet that was shot by another player
    public void spawnBullet(BulletPacket _bullet){
        // double check that the bullet recieved was not shot by the recipient of the packet
        if(_bullet.clientID != clientNumber){
            float delay = menu.time - _bullet.timeSent; 
            // predict the position based on the time sent and how long it will take to lerp to that position
            Vector2 predictedPos = _bullet.playerPosition + new Vector2(Bullet.bulletSpeed * (delay + Bullet.catchUpTime), 0).Rotated(_bullet.playerRotation);
            //Vector2 predictedPos = _bullet.playerPosition;
            player[_bullet.clientID].shoot(predictedPos, _bullet.playerRotation, delay);
        }
    }

    // used by the serialiser to update another player's transform data
    public void updatePlayer(PlayerPacket _packet){       
        // update player i 
        int i = _packet.clientID;
        Vector2 pos = new Vector2(_packet.position.x, _packet.position.y);

        pTransforms[i].newPosition = pos;        
        pTransforms[i].oldPosition = player[i].Position;        

        pTransforms[i].newRotation = _packet.rotation;
        pTransforms[i].oldRotation = player[i].Rotation;

        pTransforms[i].lerpTime = 0.0f;

        pTransforms[i].velocity = _packet.velocity;
    }

    // used by the serialiser to decrement the health of a player, including the current client
    public void decrementHealth(int _pNum){
        // if the player isn't dead
        if(!player[_pNum].isDead){
            // take damage
            player[_pNum].health--;
            // check if the player is now dead
            if(player[_pNum].health <= 0){
                player[_pNum].isDead = true; 
                menu.addChatMessage($"Player {_pNum + 1} has died!");

                if(_pNum == clientNumber){  
                    // confirm with the server that a player has died  
                    network.send(Network.hostPort, network.serverIp, _pNum, Serialiser.PacketType.PlayerDied, true); 
                }
            }
        }
    }

    public void checkIfDead(int _pNum){
        // if the player is not dead and they should be, 
        if(!player[_pNum].isDead){
            // kill the player
            menu.addChatMessage($"Player {_pNum + 1} has died!");
            player[_pNum].health = 0;
            player[_pNum].isDead = true;
        }
    }

    public void checkForWinner(){
        for(int i = 0; i < 4; i++){
            if(!player[i].isDead){                
                menu.addChatMessage($"Player {i + 1} is the last person standing!"); 
                return;
            }
        }
    }

    // removes a player upon disconnect
    public void removePlayer(int _i){
        menu.addChatMessage($"Player {_i + 1} has disconnected");
        player[_i].isDead = true;
    }
}
