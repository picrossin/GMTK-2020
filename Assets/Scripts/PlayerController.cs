﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerController : MonoBehaviour
{
    // Movement
    [Header("Movement Settings")]
    [SerializeField] [Range(1.0f, 50.0f)] private float movementSmoothingAmount = 5.0f;
    [SerializeField] [Range(0.001f, 1.0f)] private float baseFlySpeed = 0.15f;
    [SerializeField] [Range(0.001f, 1.0f)] private float flightAccelerationAmount = 0.1f;
    private bool canMove = true, isMoving = false, flying = false, flightDistanceSet = false, flightAvailable = true;
    private Vector3 movement = Vector3.zero;
    private Vector3 smoothedPosition, targetPosition;
    private Vector2 facing = Vector2.down;
    private Vector2 flightStartingPoint = Vector2.zero;
    private float currentFlightSpeed = 0f, flightDistance = 0f;
    private MovementManager movementManager;
    private string flightTrigger = "FlyDown", afterFlightTrigger = "MoveDown";
    private bool inEnemy = false;

    // Blood-lust counter
    [Header("Bloodlust Settings")]
    [SerializeField] [Range(1, 10)] private int lustMax = 6;
    [SerializeField] [Range(1, 10)] private int enemyDecreaseAmount = 3;
    private int lustCounter = 0;

    // Collisions
    [Header("Collision Settings")]
    [SerializeField] private LayerMask wallLayerMask;
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private LayerMask gateMask;
    [SerializeField] private string boxTag;
    private GameObject touchingBox = null;
    private bool canPushBox = false;
    private Vector2 boxOffset = Vector2.zero;

    // Enemy
    private GameObject enemy = null;

    // Animation
    [Header("Animation Settings")]
    [SerializeField] private GameObject downSwordIdle;
    [SerializeField] private GameObject upSwordIdle;
    [SerializeField] private GameObject leftSwordIdle;
    [SerializeField] private GameObject rightSwordIdle;
    [SerializeField] private GameObject downSwordFly;
    [SerializeField] private GameObject upSwordFly;
    [SerializeField] private GameObject leftSwordFly;
    [SerializeField] private GameObject rightSwordFly;
    [SerializeField] private Sprite deadSprite;
    [SerializeField] [Range(0.001f, 1.0f)] private float growSpeed = 0.025f;
    [SerializeField] private GameObject hat, runParticles, stepParticles, enemyKillParticles;

    private GameObject currentSword, flightSword, afterFlightSword;
    private Animator animator;
    private bool growing = true, falling = false, ascending = false, playingDeathAnimation = false;
    private ScreenShake screenShake;

    // Sound
    [Header("Sound Settings")]
    [SerializeField] private GameObject stepSound;
    [SerializeField] private GameObject flySound;
    [SerializeField] private GameObject killEnemySound;
    [SerializeField] private GameObject bonkSound;

    private GameObject flySoundInstance = null;

    private void Start()
    {
        runParticles.GetComponent<ParticleSystem>().Stop();
        stepParticles.GetComponent<ParticleSystem>().Stop();
        currentSword = downSwordIdle;
        animator = GetComponent<Animator>();
        canMove = false;
        growing = true;
        transform.localScale = new Vector3(0f, 0f, 1f);
    }

    private void Update()
    {
        if (growing)
        {
            if (transform.localScale.x < 1f || transform.localScale.y < 1f)
            {
                transform.localScale += new Vector3(growSpeed, growSpeed, 0f);
            }
            else
            {
                growing = false;
                canMove = true;
            }
        }
        else if (falling)
        {
            if (transform.localScale.x > 0f || transform.localScale.y > 0f)
            {
                transform.localScale -= new Vector3(growSpeed, growSpeed, 0f);
            }
            else
            {
                IncreaseBloodLustCounter(GetLustMax());
                falling = false;
            }
        }
        else if (ascending)
        {
            if (transform.localScale.x > 0f || transform.localScale.y > 0f)
            {
                transform.localScale -= new Vector3(growSpeed, growSpeed, 0f);
            }
            else
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
                ascending = false;
            }
        }
        else
        {
            // Get movement manager
            if (movementManager == null)
            {
                movementManager = GameObject.FindWithTag("MovementManager").GetComponent<MovementManager>();
            }

            // Get screen shake
            if (screenShake == null)
            {
                screenShake = GameObject.FindWithTag("MainCamera").GetComponent<ScreenShake>();
            }

            // Collisions
            RaycastHit2D upHit = Physics2D.Raycast(transform.position, Vector2.up, 0.55f, wallLayerMask | gateMask);
            RaycastHit2D downHit = Physics2D.Raycast(transform.position, Vector2.down, 0.55f, wallLayerMask | gateMask);
            RaycastHit2D rightHit = Physics2D.Raycast(transform.position, Vector2.right, 0.55f, wallLayerMask | gateMask);
            RaycastHit2D leftHit = Physics2D.Raycast(transform.position, Vector2.left, 0.55f, wallLayerMask | gateMask);

            Debug.DrawRay(transform.position, new Vector2(0, .6f));
            Debug.DrawRay(transform.position, new Vector2(0, -.6f));
            Debug.DrawRay(transform.position, new Vector2(-.6f, 0));
            Debug.DrawRay(transform.position, new Vector2(.6f, 0));


            // Get input (ugly, but necessary for tile-based movement)
            if (!isMoving)
            {
                canPushBox = false;
                if (Input.GetButtonDown("Restart"))
                {
                    IncreaseBloodLustCounter(GetLustMax());
                }
                else if (Input.GetButtonDown("Up"))
                {
                    animator.SetTrigger("MoveUp");
                    flightTrigger = "FlyUp";
                    afterFlightTrigger = "MoveUp";
                    currentSword.GetComponent<SpriteRenderer>().enabled = false;
                    currentSword = upSwordIdle;
                    flightSword = upSwordFly;
                    afterFlightSword = upSwordIdle;
                    SetInput(upHit, new Vector2(0, 1));
                    if (touchingBox != null && !touchingBox.GetComponent<Box>().upHit)
                        canPushBox = true;
                }
                else if (Input.GetButtonDown("Down"))
                {
                    animator.SetTrigger("MoveDown");
                    flightTrigger = "FlyDown";
                    afterFlightTrigger = "MoveDown";
                    currentSword.GetComponent<SpriteRenderer>().enabled = false;
                    currentSword = downSwordIdle;
                    flightSword = downSwordFly;
                    afterFlightSword = downSwordIdle;
                    SetInput(downHit, new Vector2(0, -1));
                    if (touchingBox != null && !touchingBox.GetComponent<Box>().downHit)
                        canPushBox = true;
                }
                else if (Input.GetButtonDown("Right"))
                {
                    animator.SetTrigger("MoveRight");
                    flightTrigger = "FlyRight";
                    afterFlightTrigger = "MoveRight";
                    currentSword.GetComponent<SpriteRenderer>().enabled = false;
                    currentSword = rightSwordIdle;
                    flightSword = rightSwordFly;
                    afterFlightSword = rightSwordIdle;
                    SetInput(rightHit, new Vector2(1, 0));
                    if (touchingBox != null && !touchingBox.GetComponent<Box>().rightHit)
                        canPushBox = true;
                }
                else if (Input.GetButtonDown("Left"))
                {
                    animator.SetTrigger("MoveLeft");
                    flightTrigger = "FlyLeft";
                    afterFlightTrigger = "MoveLeft";
                    currentSword.GetComponent<SpriteRenderer>().enabled = false;
                    currentSword = leftSwordIdle;
                    flightSword = leftSwordFly;
                    afterFlightSword = leftSwordIdle;
                    SetInput(leftHit, new Vector2(-1, 0));
                    if (touchingBox != null && !touchingBox.GetComponent<Box>().leftHit)
                        canPushBox = true;
                }
                else
                {
                    movement = Vector3.zero;
                }
            }

            // Look for enemies
            RaycastHit2D enemyLook = Physics2D.Raycast(transform.position, facing, Mathf.Infinity, wallLayerMask | enemyMask);
            if (enemyLook && enemyLook.transform.gameObject.layer == LayerMaskToLayer(enemyMask) && flightAvailable && !isMoving)
            {
                enemy = enemyLook.transform.gameObject;
                flying = true;

                if (!flightDistanceSet)
                { 
                    animator.SetTrigger(flightTrigger);
                    runParticles.GetComponent<ParticleSystem>().Play();
                    flySoundInstance = Instantiate(flySound);

                    if (facing.x == 1)
                    {
                        runParticles.transform.rotation = Quaternion.Euler(0, 0, 0);
                    }
                    else if (facing.x == -1)
                    {
                        runParticles.transform.rotation = Quaternion.Euler(0, 0, 180);
                    }
                    else if (facing.y == 1)
                    {
                        runParticles.transform.rotation = Quaternion.Euler(0, 0, 90);
                    }
                    else if (facing.y == -1)
                    {
                        runParticles.transform.rotation = Quaternion.Euler(0, 0, -90);
                    }

                    currentSword.GetComponent<SpriteRenderer>().enabled = false;
                    currentSword = flightSword;
                    flightDistance = Vector2.Distance(transform.position, enemy.transform.position);
                    flightStartingPoint = transform.position;
                    flightDistanceSet = true;
                }
            }

            // Check for movement
            if (canMove && !isMoving && movement != Vector3.zero && !flying &&
                (touchingBox == null || (touchingBox != null && canPushBox)))
            {
                GameObject stepInstance = Instantiate(stepSound, transform.position, Quaternion.identity);
                stepInstance.GetComponent<AudioSource>().pitch = Random.Range(0.9f, 1.1f);

                isMoving = true;
                movementManager.Move();
                flightAvailable = true;
                targetPosition = transform.position + movement;
                smoothedPosition = transform.position;
            }

            // Move to another space normally
            if (canMove && isMoving && !flying)
            {
                if (Vector3.Distance(smoothedPosition, targetPosition) > 0.1f)
                {
                    smoothedPosition = Vector3.Lerp(smoothedPosition, targetPosition, movementSmoothingAmount * Time.deltaTime);
                    transform.position = smoothedPosition;

                    if (touchingBox != null && canPushBox)
                    {
                        boxOffset = targetPosition - smoothedPosition;
                        boxOffset.Normalize();
                        touchingBox.transform.position = transform.position +
                            new Vector3(boxOffset.x, boxOffset.y);
                    }
                }
                else
                {
                    stepParticles.GetComponent<ParticleSystem>().Play();
                    IncreaseBloodLustCounter(1);
                    transform.position = targetPosition;
                    movement = Vector3.zero;
                    isMoving = false;

                    if (touchingBox != null && canPushBox)
                    {
                        touchingBox.transform.position = transform.position +
                            new Vector3(boxOffset.x, boxOffset.y);
                    }
                }
            }
            // Fly to the enemy if necessary
            else if (flying)
            {
                if (Vector2.Distance(transform.position, flightStartingPoint) >= flightDistance)
                {
                    transform.position = new Vector3(enemy.transform.position.x, enemy.transform.position.y, transform.position.z);
                    Destroy(enemy);
                    DecreaseBloodlustCounter(enemyDecreaseAmount);

                    if (flySoundInstance)
                    {
                        flySoundInstance.GetComponent<AudioSource>().Stop();
                    }
                    Instantiate(killEnemySound);
                    Instantiate(enemyKillParticles, transform.position, enemyKillParticles.transform.rotation);

                    inEnemy = false;
                    runParticles.GetComponent<ParticleSystem>().Stop();
                    screenShake.ShakeScreen(0.2f, 0.1f, 2);
                    movementManager.Move();
                    movement = Vector3.zero;
                    flightAvailable = false;
                    isMoving = false;
                    flying = false;
                    flightDistanceSet = false;
                    animator.SetTrigger(afterFlightTrigger);
                    currentFlightSpeed = 0f;
                    currentSword.GetComponent<SpriteRenderer>().enabled = false;
                    currentSword = afterFlightSword;
                }
                else if (facing.x == 1 && rightHit && !inEnemy)
                {
                    ResetAfterFlight(rightHit, new Vector3(1, 0));
                    animator.SetTrigger("MoveRight");
                    currentSword.GetComponent<SpriteRenderer>().enabled = false;
                    currentSword = rightSwordIdle;
                }
                else if (facing.x == -1 && leftHit && !inEnemy)
                {
                    ResetAfterFlight(leftHit, new Vector3(-1, 0));
                    animator.SetTrigger("MoveLeft");
                    currentSword.GetComponent<SpriteRenderer>().enabled = false;
                    currentSword = leftSwordIdle;
                }
                else if (facing.y == 1 && upHit && !inEnemy)
                {
                    ResetAfterFlight(upHit, new Vector3(0, 1));
                    animator.SetTrigger("MoveUp");
                    currentSword.GetComponent<SpriteRenderer>().enabled = false;
                    currentSword = upSwordIdle;
                }
                else if (facing.y == -1 && downHit && !inEnemy)
                {
                    ResetAfterFlight(downHit, new Vector3(0, -1));
                    animator.SetTrigger("MoveDown");
                    currentSword.GetComponent<SpriteRenderer>().enabled = false;
                    currentSword = downSwordIdle;
                }
                else
                {
                    isMoving = true;
                    currentFlightSpeed += flightAccelerationAmount;
                    transform.position += new Vector3(facing.x, facing.y, 0) * currentFlightSpeed;
                }
            }

            // Set correct sword
            currentSword.GetComponent<SpriteRenderer>().enabled = true;
        }
    }

    public void SetLustMax(int value)
    {
        lustMax = value;
    }

    public int GetLustMax()
    {
        return lustMax;
    }

    public int GetCurrentLust()
    {
        return lustCounter;
    }

    public void DecreaseBloodlustCounter(int value)
    {
        lustCounter = Mathf.Max(0, lustCounter - value);
        screenShake.ShakeScreen(0.2f, 0.1f, 2);
        CheckReload();
    }

    public void IncreaseBloodLustCounter(int value)
    {
        lustCounter = Mathf.Min(lustMax, lustCounter + value);
        CheckReload();
    }

    public bool IsFlying()
    {
        return flying;
    }

    public void SetFalling(bool value)
    {
        falling = value;
    }

    public void SetAscending(bool value)
    {
        ascending = value;
    }

    private void SetInput(RaycastHit2D hit, Vector2 direction)
    {
        touchingBox = CheckBoxCollisions(hit);

        if (!hit || touchingBox != null)
        {
            movement = direction;
            facing = direction;
        }
    }

    private GameObject CheckBoxCollisions(RaycastHit2D hit)
    {
        GameObject returnBox = null;
        if (hit && hit.transform.gameObject.tag == boxTag)
            returnBox = hit.transform.gameObject;
        return returnBox;
    }

    private void ResetAfterFlight(RaycastHit2D hit, Vector3 displacement)
    {
        screenShake.ShakeScreen(0.2f, 0.1f, 2);

        transform.position = new Vector3(hit.transform.position.x - displacement.x, hit.transform.position.y - displacement.y, transform.position.z);
        IncreaseBloodLustCounter(1);

        if (Mathf.Floor(Vector2.Distance(transform.position, flightStartingPoint)) >= Mathf.Floor(flightDistance))
        {
            transform.position = new Vector3(enemy.transform.position.x, enemy.transform.position.y, transform.position.z);
        }

        if (flySoundInstance)
        {
            flySoundInstance.GetComponent<AudioSource>().Stop();
        }
        Instantiate(bonkSound);

        runParticles.GetComponent<ParticleSystem>().Stop();
        movementManager.Move();
        movement = Vector3.zero;
        flightAvailable = false;
        isMoving = false;
        flying = false;
        flightDistanceSet = false;
        currentFlightSpeed = 0f;
    }

    private void CheckReload()
    {
        if (lustCounter >= lustMax)
        {
            StartCoroutine(DeathAnimation());
        }
    }

    private IEnumerator DeathAnimation()
    {
        if (!playingDeathAnimation)
        {
            playingDeathAnimation = true;

            screenShake.ShakeScreen(0.3f, 0.3f, 2);

            flying = false;
            isMoving = false;
            canMove = false;
            Instantiate(hat, transform.position - new Vector3(0, 0, 1), Quaternion.identity);
            currentSword.AddComponent<RandomVelocity>();
            animator.enabled = false;
            GetComponent<SpriteRenderer>().sprite = deadSprite;
            yield return new WaitForSeconds(1f);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
        yield return new WaitForSeconds(1f);
    }

    private int LayerMaskToLayer(LayerMask layerMask)
    {
        int layerNumber = 0;
        int layer = layerMask.value;

        while (layer > 0)
        {
            layer >>= 1;
            layerNumber++;
        }

        return layerNumber - 1;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if ((collision.tag == "Enemy" || collision.tag == "ShootingEnemy" || collision.tag == "MovingEnemy") && collision.gameObject == enemy)
        {
            inEnemy = true;
        }
    }
}
