using Godot;
using System;
using System.Linq;
public class Bullet : RigidBody2D
{
    public const float catchUpTime = 0.2f; // time taken to get from the player's weapon position to the predicted position of the bullet    
    public const float bulletSpeed = 600.0f;
    float catchUpTimer = 0.0f;
    public float lifeTime = 1.0f; // how long the bullet will last before destroying itself
    public int owner; // player that shot the bullet
    public bool damageEnabled = true; 
    public Vector2 predictedPos;
    bool once = true;
    // Called when the node enters the scene tree for the first time.

    public override void _Process(float _delta)
    {        
        catchUpTimer += _delta;
        // delete the bullet after the lifetime has expired
        if(lifeTime < 0.0f){
            QueueFree();
        }else{
            lifeTime -= _delta;
        }        
        
        if(catchUpTimer > catchUpTime && once){
            LinearVelocity = new Vector2(bulletSpeed, 0).Rotated(Rotation);
            once = false;
        }
    }

    public void OnEnter(Node2D _collision){             
        // if the player has collided with a bullet 
        if(_collision.IsInGroup("Player")){
            Player player = (Player)_collision;
            // and the player is not the owner of the bullet
            if(owner != player.playerNumber){
                // disable damage
                damageEnabled = false;     
            }
        } 
    }

}
