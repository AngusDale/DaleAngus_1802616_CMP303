using Godot;
using System;

public class Player : KinematicBody2D
{       
    public int playerNumber;
    public bool isClient = false;    

    public Vector2 velocity;
    const float moveSpeed = 300.0f;
    bool canShoot = true;
    public bool isDead = false;
    public int health = 35;    
    float shootTimer = 0.0f;
    const float fireRate = 0.15f;  
    private PackedScene bulletScene = (PackedScene)GD.Load("res://Bullet.tscn");
    public Sprite weapon;
    Game game;
    CollisionShape2D hitBox;
    const float invulnTime = 0.1f;
    float invulnTimer = 0.0f;
    public bool movementEnabled = true;
    public override void _Ready()
    {
        game = GetNode<Game>("../Game");
        velocity = new Vector2(0, 0);
        weapon = GetNode<Sprite>("Weapon");
        hitBox = GetNode<CollisionShape2D>("CollisionShape2D");
    }

    public override void _Process(float delta)
    {
        if(isDead){
            Hide();
            hitBox.Disabled = true;
        }
    }

    public override void _PhysicsProcess(float delta){
        if(isClient && !isDead){
            
            invulnTimer += delta;
            shootTimer += delta; 
            
            // player input
            if(movementEnabled){
                Vector2 vel = new Vector2();

                if(Input.IsActionPressed("up"))
                    vel.y -=1;
                if(Input.IsActionPressed("down"))
                    vel.y +=1;
                if(Input.IsActionPressed("left"))
                    vel.x -=1;
                if(Input.IsActionPressed("right"))
                    vel.x +=1;
                if(Input.IsActionPressed("LMB") && shootTimer > fireRate){     
                    shootTimer = 0.0f;                
                    game.sendBullet();
                    shoot();               
                }
                
                // apply the movement
                vel = MoveAndSlide(vel * moveSpeed);
                velocity = vel;
                // rotate the player to the mouse position
                LookAt(GetGlobalMousePosition());
            }                                                   
        }
    } 

    public void shoot(Vector2 _position, float _rotation, float _delay){
        // called when a different player to the client needs to shoot
        Bullet bullet = (Bullet)bulletScene.Instance();
        bullet.predictedPos = _position;
        bullet.Position = weapon.GlobalPosition;        
        bullet.lifeTime -= _delay;
        // pythagoras to calculate the distance between the predicted pos and the starting pos
        float distance = Mathf.Sqrt(Mathf.Pow(bullet.Position.x - bullet.predictedPos.x, 2) + Mathf.Pow(bullet.Position.y - bullet.predictedPos.y, 2));
        // calculate the speed the bullet needs to travel to catch up to the prediction
        float tempSpeed = distance / Bullet.catchUpTime;

        bullet.Rotation = _rotation;
        bullet.owner = playerNumber;

        bullet.LinearVelocity = new Vector2(tempSpeed, 0).Rotated(Rotation);
        // add the bullet to the scene
        GetTree().GetRoot().CallDeferred("add_child", bullet);
    }

     public void shoot(){
        // the client's player shoot
        Bullet bullet = (Bullet)bulletScene.Instance();
        bullet.Position = weapon.GlobalPosition;
        bullet.Rotation = Rotation;
        bullet.owner = playerNumber;
        bullet.LinearVelocity = new Vector2(Bullet.bulletSpeed, 0).Rotated(Rotation);
        // add the bullet to the scene
        GetTree().GetRoot().CallDeferred("add_child", bullet);
        bullet.AddToGroup("Bullet", true);
    }

    public void setTexture(int _col){
        Sprite sprite = GetNode<Sprite>("Sprite"); 
        switch(_col){
            case 0:
                sprite.Texture = (Texture)GD.Load("res://Sprites/Blue_Square.png");                
                break;
            case 1:
                sprite.Texture = (Texture)GD.Load("res://Sprites/Red_Square.png");
                break;
            case 2:
                sprite.Texture = (Texture)GD.Load("res://Sprites/Yellow_Square.png");
                break;
            case 3:
                sprite.Texture = (Texture)GD.Load("res://Sprites/Green_Square.png");
                break;
        }
    }

    // collision response function
    public void OnEnter(Node _collision){
        // if the player has collided with a bullet
        if(_collision.IsInGroup("Bullet") && health > 0 & invulnTimer > invulnTime){
            Bullet b = (Bullet)_collision;
            if(b.damageEnabled && b.owner != playerNumber){
                // send that a player has been hit by a bullet to the server
                game.sendHit(playerNumber);
                //GD.Print(health);
                invulnTimer = 0.0f;
            }
        }
    }
}
