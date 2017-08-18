using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : Entity 
{
    public int hp = 10;

    public float jumpSpeed = 5f;
    public float moveSpeed = 5f;
    public float airControlRatio = .7f;

	private bool grounded = false;
    private bool wallSliding = false;

    private Animator anim;
    public Rigidbody2D rb2d;

	public bool swinging = false;

    //if this is true, the player is invincible to enemy attacks and will trigger a riposte on hit
    //(todo)
    public bool parrying;

    public bool attackCooldown = false;

    public float ROLL_VELOCITY = -4f;
    bool fastFalling = false;

    List<KeyCode> forcedInputs;

    public int maxAirJumps = 0;
    private int airJumps;

    [HideInInspector] public GameObject platformTouching;

    GameObject currentHurtbox;
    public GameObject hurtboxes;

    public bool comboWindow;

    SpriteRenderer spr;
    Material defaultMaterial;
    Material cyanMaterial;
    int flashTimes = 5;

    public bool invincible = false;

    public bool inHitstop = false;

	void Start () 
	{
        anim = GetComponent<Animator>();
        rb2d = GetComponent<Rigidbody2D>();
        swinging = false;
        grounded = false;
        anim.SetBool("falling", true);

        forcedInputs = new List<KeyCode>();

        airJumps = maxAirJumps;

        CloseAllHurtboxes();

        spr = this.GetComponent<SpriteRenderer>();
        defaultMaterial = spr.material;
        cyanMaterial = Resources.Load<Material>("Shaders/CyanFlash");
    }

	void FixedUpdate () 
	{
        Jump();
		Attack();
        Move();
        if (rb2d.velocity.y < ROLL_VELOCITY) {
            this.fastFalling = true;
        } else {
            this.fastFalling = false;
        }
    }

    public void HitGround(Collision2D col) {
        InterruptAttack();
        if (col.transform.position.y < this.transform.position.y) {
            grounded = true;
            anim.SetBool("grounded", true);
            //cancel an aerial attack
            StopSwinging();
            //anim.SetBool("grounded", true);
            StopFalling();
            StopWallSliding();
            
            if (fastFalling) {
                if (HorizontalInput()) {
                    anim.SetTrigger("roll");
                } else {
                    anim.SetTrigger("hardLand");
                }
            }
        }
        this.airJumps = maxAirJumps;

        //track if they're on a passthrough, one-way platform
        platformTouching = col.collider.gameObject;
    }

    public void LeaveGround(Collision2D col) {
        grounded = false;
        anim.SetBool("grounded", false);
        if (rb2d.velocity.y < 0) {
            anim.SetTrigger("fall");
        }
        platformTouching = null;
    }

    public void HitWall(Collision2D col) {
        StopFalling();
        StartWallSliding();
        this.airJumps = maxAirJumps;
    }

    public void StayOnWall(Collision2D col) {

    }

    public void LeaveWall(Collision2D col) {
        StopWallSliding();
        anim.SetBool("falling", true);
    }

    void Jump()
    {
    if (!((Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.UpArrow)) && 
            (grounded || wallSliding || airJumps >= 0))) {
                return;
            }

        if (!grounded || !wallSliding) {
            airJumps--;
        }
        if (wallSliding)
        {
            //jump away from the current wall, and freeze player inputs so they don't get glued back
            StartCoroutine(WallJump());
        } else
        {
            rb2d.velocity = new Vector2(rb2d.velocity.x, jumpSpeed);
            InterruptAttack();
        }
        anim.SetBool("grounded", false);
        anim.SetTrigger("jump");
        anim.SetBool("falling", false);
    }

	void Attack()
	{
        if (attackCooldown || parrying) {
            return;
        }
        if (Input.GetKeyDown(KeyCode.Z) && Input.GetKey(KeyCode.DownArrow) && !swinging)
        {
            Parry();
        } else if (Input.GetKeyDown(KeyCode.Z) && CanGroundAttack())
		{
            anim.SetTrigger("groundAttack");
            rb2d.velocity = new Vector2(0, rb2d.velocity.y);
		} else if (Input.GetKeyDown(KeyCode.Z) && !grounded && !swinging && !wallSliding){
            AirAttack();
        }
        else if (Input.GetKeyDown(KeyCode.D)) {
            Dodge();
        }
	}

    void Parry() {
        anim.SetTrigger("parry");
    }

    /**
    This adds a small force opposite the wall and freezes the player for a moment so they don't 
    immediately move back to it.
     */
    IEnumerator WallJump()
    {
        anim.SetBool("wallSliding", false);
        this.wallSliding = false;
        anim.SetTrigger("jump");
        Freeze();
        //push up and away from the wall
        rb2d.velocity = new Vector2(-4 * (facingRight ? 1 : -1), 1.2f * jumpSpeed);
        yield return new WaitForSeconds(.1f);
        UnFreeze();
    }

    void Move()
    {
        float h = Input.GetAxis("Horizontal");

        //stop the player if they're moving on the ground
        if (Mathf.Abs(h) < 1 && grounded)
        {
            rb2d.velocity = new Vector2(0, rb2d.velocity.y);
            anim.SetBool("running", false);
        }

        if (grounded && HorizontalInput() && !swinging && !frozen)
        {
            if (Input.GetKey(KeyCode.RightArrow))
            {
                rb2d.velocity = new Vector2(moveSpeed, rb2d.velocity.y);
            }
            else if (Input.GetKey(KeyCode.LeftArrow))
            {
                rb2d.velocity = new Vector2(-moveSpeed, rb2d.velocity.y);
            }

            if ((rb2d.velocity.x != 0 || HorizontalInput()) && grounded)
            {
                anim.SetBool("running", true);
            }
        } else if (!grounded && HorizontalInput() && !swinging && !frozen)
        {
            //in the air, lessen control but maintain existing airspeed if moving at max
            if (Input.GetKey(KeyCode.RightArrow) && rb2d.velocity.x < moveSpeed)
            {
                rb2d.velocity = new Vector2(rb2d.velocity.x + moveSpeed * airControlRatio, rb2d.velocity.y);
            }
            else if (Input.GetKey(KeyCode.LeftArrow) && rb2d.velocity.x > (moveSpeed * -1))
            {
                rb2d.velocity = new Vector2(-moveSpeed * airControlRatio, rb2d.velocity.y);
            }
        }

        if (!facingRight && rb2d.velocity.x > 0 && !Input.GetKey(KeyCode.LeftArrow))
        {
            Flip();
        }
        else if (facingRight && rb2d.velocity.x < 0 && !Input.GetKey(KeyCode.RightArrow))
        {
            Flip();
        }

        //stop the sliding animation if needed
        if (wallSliding) {
            if (Mathf.Abs(rb2d.velocity.y) < 0.2) {
                anim.SetTrigger("wallstick");
            } else {
                anim.SetTrigger("wallunstick");
            }
        }

        //if they press down to drop through a platform
        if (Input.GetKeyDown(KeyCode.DownArrow)) {
            //if they can actually drop through a platform
            if (platformTouching != null && platformTouching.GetComponent<PlatformEffector2D>() != null) {
                //disable the platform collider for a second
                platformTouching.GetComponent<PlatformController>().StartDisable();
                rb2d.AddForce(new Vector2(0, -100f));
                anim.SetTrigger("fall");
                anim.SetBool("grounded", false);
                this.grounded = false;
            }
        }
    }

    void StartFalling()
    {
        anim.SetBool("falling", true);
    }

    void StopFalling()
    {
        anim.SetBool("falling", false);
    }

    void StopWallSliding()
    {
        anim.SetBool("wallSliding", false);
        anim.SetBool("falling", true);
        this.wallSliding = false;
    }

    void StartWallSliding()
    {
        anim.SetBool("wallSliding", true);
        anim.SetBool("falling", false);
        this.wallSliding = true;
    }
		
    bool HorizontalInput()
    {
        return Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.LeftArrow);
    }

    void StartSwinging() {
        this.swinging = true;
    }

    void StopSwinging() {
        this.swinging = false;
    }

    public void AttackCooldown(float seconds) {
        this.comboWindow = false;
        StartCoroutine(StartAttackCooldown(seconds));
    }

    private IEnumerator StartAttackCooldown(float seconds) {
        this.attackCooldown = true;
        yield return new WaitForSeconds(seconds);
        this.attackCooldown = false;
    }

    void ResetAttackCooldown() {
        this.attackCooldown = false;
    }

    void AirAttack() {
        anim.SetTrigger("airAttack");
    }

    public void StartParrying() {
        this.parrying = true;
    }
    public void StopParrying() {
        this.parrying = false;
    }

    void StartForcing(KeyCode keyCode) 
    {
        //don't want to add duplicates
        if (!this.forcedInputs.Contains(keyCode)) {
            this.forcedInputs.Add(keyCode);
        }
    }
    void StopForcing(KeyCode keyCode) {
        this.forcedInputs.Remove(keyCode);
    }
    void StopForcingAll() {
        this.forcedInputs.Clear();
    }

    public void Freeze() {
        this.frozen = true;
    }
    public void UnFreeze() {
        this.frozen = false;
    }

    public void InterruptAttack() {
        //this should always be called
        this.CloseAllHurtboxes();
        this.CloseComboWindow();
        //right now you can jump cancel parries, but it could be a bit OP
        if (!swinging && !parrying) return;
        this.swinging = false;
        this.parrying = false;
        this.UnFreeze();
        this.attackCooldown = false;
    }

    void resetJumps() {
        airJumps = maxAirJumps;
    }

    //called from animation event
    public void OpenHurtbox(string hurtboxName) {
        //find the jab1 object and activate it
        foreach (Transform hurtbox in hurtboxes.GetComponentInChildren<Transform>()) {
            if (hurtbox.name.Equals(hurtboxName)) {
                this.currentHurtbox = hurtbox.gameObject;
                hurtbox.GetComponent<BoxCollider2D>().enabled = true;
                return;
            }
        }
        Debug.LogError(hurtboxName + "not found");
    }

    public void CloseHurtbox(string hurtboxName) {
        this.currentHurtbox.GetComponent<BoxCollider2D>().enabled = false;
        this.currentHurtbox = null;
    }

    public void CloseAllHurtboxes() {
        foreach (Transform hurtbox in hurtboxes.GetComponentInChildren<Transform>()) {
            if (hurtbox.GetComponent<BoxCollider2D>().enabled) {
                hurtbox.GetComponent<BoxCollider2D>().enabled = false;
            } 
        }
    }

    public void OpenComboWindow() {
        this.comboWindow = true;
    }

    public void CloseComboWindow() {
        this.comboWindow = false;
    }

    private bool CanGroundAttack() {
        if (!grounded) return false;
        //fix the down arrow check, combine inputs more gracefully
        return (!swinging && !Input.GetKey(KeyCode.DownArrow)) || comboWindow;
    }

    public void CyanSprite() {
        spr.material = cyanMaterial;
    }

    public void WhiteSprite() {
        spr.material = defaultMaterial;
    }

    IEnumerator Hurt(int flashes) {
        SetInvincible(true);
        CyanSprite();
        yield return new WaitForSeconds(.07f);
        WhiteSprite();
        yield return new WaitForSeconds(.07f);
        if (flashes > 0) {
            StartCoroutine(Hurt(--flashes)); //;^)
        } else {
            SetInvincible(false);
        }
    }

    void StartHurting(int dmg) {
        this.hp -= dmg;
        if (this.hp <= 0) {
            Die();
            return;
        }
        StartCoroutine(Hurt(flashTimes));
    }

    public void OnMonsterHit(Collider2D boneHurtingCollider) {
        if (parrying) {
            if (boneHurtingCollider.GetComponent<Projectile>()) {
                boneHurtingCollider.GetComponent<Projectile>().Reflect();
            } else if (boneHurtingCollider.GetComponent<Enemy>()) {
                if (boneHurtingCollider.tag == "enemyHurtbox") {
                    Riposte();
                }
            }

            return;
        }

        if (invincible || inHitstop) return;

        int dmg;
        if (boneHurtingCollider.GetComponent<HurtboxController>() != null) {
            dmg = boneHurtingCollider.GetComponent<HurtboxController>().damage;
        } else {
            dmg = 1;
        }
        this.StartHurting(dmg);
    }

    public void OnEnvDamage(Collider2D boneHurtingCollider) {
        if (invincible) return;
        int dmg = 1;
        if (boneHurtingCollider.GetComponent<HurtboxController>() != null) {
            dmg = boneHurtingCollider.GetComponent<HurtboxController>().damage;
        }
        this.StartHurting(dmg);
    }

    public void SetInvincible(bool b) {
        this.invincible = b;
    }

    public void Die() {
        this.Freeze();
        FreezeInSpace();
        SetInvincible(true);
        anim.SetBool("dead", true);
        anim.SetTrigger("die");
    }

    public void Hide() {
        spr.color = new Color(0, 0, 0, 0);
    }

    public void Dodge() {
        if (frozen || swinging || attackCooldown || !grounded) return;
        anim.SetTrigger("dodge");
    }

    //animation events :^)
    public void StartDodging() {
        this.SetInvincible(true);
        this.Freeze();
    }

    public void StopDodging() {        
        this.SetInvincible(false);
        this.UnFreeze();
        AttackCooldown(.2f);
    }

    void Riposte() {
        //hitstop, flashy hitmarker, freeze, set animation trigger, all that good stuff
    }
}
