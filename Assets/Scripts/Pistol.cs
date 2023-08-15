using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(Animator), typeof(AudioSource))]
public class Pistol : XRGrabInteractable
{
    [Header("Settings")]
    [SerializeField] private float m_CartridgeEjectionPower = 20f;      // Used for ejecting rounds with bullets & propellant still intact. 
    [SerializeField] private float m_MagazineEjectionPower = 25f;
    [SerializeField] private Vector3 m_CartridgeEjectionRotationOffset = new Vector3(90f, 0, 0);

    [Header("Model References")]
    [SerializeField] private GameObject m_MagazineModel;

    [Header("Location References")]
    [SerializeField] private Transform m_BulletSpawnPoint;
    [SerializeField] private Transform m_CartridgeEjectionPoint;
    [SerializeField] private Transform m_MagazineEjectionPoint;
    [SerializeField] private Transform m_MagazineEjectionDirection;
    [SerializeField] private Transform m_MagazineReleasePoint;

    [Header("Prefab References")]
    [SerializeField] private GameObject m_CartridgePrefab;
    [SerializeField] private GameObject m_MagazinePrefab;

    [Header("Particle System References")]
    [SerializeField] private ParticleSystem m_MuzzleFlashVFX;
    [SerializeField] private ParticleSystem m_CartridgeEjectionVFX;

    [Header("SFX")]
    [SerializeField] private AudioClip m_SFXGunShot;
    [SerializeField] private AudioClip m_SFXEmptyClick;
    [SerializeField] private AudioClip m_SFXInsertMagazine;
    [SerializeField] private AudioClip m_SFXReleaseMagazine;

    // Private component references
    private Animator m_Animator;
    private AudioSource m_AudioSource;
    private MyActionBasedController m_Controller;

    [Header("Private members")]
    /// <summary>
    /// True if player is holding onto this pistol and false if not.
    /// NOTE: I don't think this is the best way to handle the usage of this object but it'll do for now...
    /// </summary>
    private bool m_HasInsertedMagazine;
    private Magazine.MagazineInfo m_MagazineInfo;
    private bool m_HasRoundInChamber;

    public Magazine.MagazineInfo GetMagazineInfo() { return m_MagazineInfo; }
    public bool HasRoundInChamber { get { return m_HasRoundInChamber; } }

    override protected void Awake()
    {
        base.Awake();
        m_Animator = GetComponent<Animator>();
        m_AudioSource = GetComponent<AudioSource>();

        m_MagazineModel.SetActive(m_HasInsertedMagazine);
    }

    private void OnTriggerEnter(Collider other)
    {
        // If there is no loaded magazine and a mag is detected, load it into the pistol.
        if (!m_HasInsertedMagazine)
        {
            Magazine detectedMagazine = other.GetComponent<Magazine>();
            if (detectedMagazine != null && detectedMagazine.CanBeLoaded && !detectedMagazine.WasReleasedFromGun)
            {
                Debug.Log("Detected magazine. Triggering load mag anim...");
                m_MagazineInfo = detectedMagazine.GetMagazineInfo();

                // Play load magazine animation (which plays load mag sfx via anim event)
                m_Animator.SetTrigger("LoadMagazine");
                m_MagazineModel.SetActive(true);
                Destroy(other.gameObject);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!m_HasInsertedMagazine)
        {
            Magazine detectedMagazine = other.GetComponent<Magazine>();
            if (detectedMagazine != null && detectedMagazine.WasReleasedFromGun)
            {
                Debug.Log("Setting mag was released from gun...");
                detectedMagazine.WasReleasedFromGun = false;
            }
        }
    }

    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        base.OnSelectEntered(args);
        XRBaseControllerInteractor controllerInteractor = args.interactorObject as XRBaseControllerInteractor;
        m_Controller = controllerInteractor.xrController as MyActionBasedController;

        if (m_Controller != null)
        {
            // Subscribing to input events...
            m_Controller.primaryButtonTap.action.performed += context => EjectMagazine();
            m_Controller.primaryButtonLongPress.action.performed += context => ReleaseMagazine();
        }
    }

    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        base.OnSelectExited(args);

        if (m_Controller != null)
        {
            // Unsubscribing to input events...
            m_Controller.primaryButtonTap.action.performed -= context => EjectMagazine();
            m_Controller.primaryButtonLongPress.action.performed -= context => ReleaseMagazine();
        }
    }

    public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
    {
        base.ProcessInteractable(updatePhase);

        //if (updatePhase == XRInteractionUpdateOrder.UpdatePhase.Dynamic)
        //{
            
        //}
    }

    public void PullTrigger(ActivateEventArgs args)
    {
        if (m_HasRoundInChamber)
        {
            // If the magazine still has rounds, chamber it
            if (m_HasInsertedMagazine && m_MagazineInfo.HeldRounds > 0)
            {
                --m_MagazineInfo.HeldRounds;
                m_HasRoundInChamber = true;
            }
            else
            {
                m_HasRoundInChamber = false;
            }
            m_Animator.SetBool("HasRoundInChamber", m_HasRoundInChamber);

            // Play pistol fire sfx, muzzle flash vfx and trigger controller haptics
            m_AudioSource.PlayOneShot(m_SFXGunShot);
            if (m_MuzzleFlashVFX)
            {
                m_MuzzleFlashVFX.Stop();
                m_MuzzleFlashVFX.Play();
            }
            m_Controller.SendHapticImpulse(1f, 0.5f);

            // Play animation of the slide/bolt moving rearwards from recoil and then moving back to position
            m_Animator.SetTrigger("Fire");

            // Shoot a raycast to determine if we hit anything.

            // Handle spawning bullet hole decal or dealing damage to hit entities.
        }
        else
        {
            // Play empty click sfx
            m_AudioSource.clip = m_SFXEmptyClick;
            m_AudioSource.Play();
        }
    }

    /// <summary>
    /// Removes the round in the chamber and spawns the bullet as a gameobject, ejecting it from the pistol.
    /// Bullets despawn after x seconds.
    /// </summary>
    public void EjectBullet()
    {
        //Debug.Log("Ejecting bullet...");
        m_HasRoundInChamber = false;

        // Cancels function if ejection slot hasn't been set or there's no casing
        if (!m_CartridgePrefab || !m_CartridgeEjectionPoint)
        {
            Debug.LogWarning("No shell casing prefab or shell casing ejection point assigned.");
            return;
        }

        // Create the casing
        GameObject ejectedBullet = Instantiate<GameObject>(m_CartridgePrefab, m_CartridgeEjectionPoint.position, m_CartridgeEjectionPoint.rotation);
        ejectedBullet.transform.Rotate(m_CartridgeEjectionRotationOffset);  // By right, all models stand upright, including shell casings. To orient correctly, apply the rotation offset.
        // Add force on casing to push it out
        Rigidbody bulletRB = ejectedBullet.GetComponent<Rigidbody>();
        bulletRB.AddExplosionForce(UnityEngine.Random.Range(m_CartridgeEjectionPower * 0.7f, m_CartridgeEjectionPower),
                                        (m_CartridgeEjectionPoint.position - m_CartridgeEjectionPoint.right * 0.3f - m_CartridgeEjectionPoint.up * 0.6f),
                                        1f);
        // Add torque to make casing spin in random direction
        bulletRB.AddTorque(new Vector3(0, UnityEngine.Random.Range(100f, 500f), UnityEngine.Random.Range(100f, 1000f)), ForceMode.Impulse);

        // Destroy casing after X seconds
        Destroy(ejectedBullet, 60f);
    }

    /// <summary>
    /// This function is to be triggered via an animation event.
    /// It could also be called when the slide is manually pulled back. (see GunSlide.cs)
    /// </summary>
    public void EjectCasing()
    {
        if (m_CartridgeEjectionVFX)
        {
            m_CartridgeEjectionVFX.Emit(1);
        }
    }

    /// <summary>
    /// Release the mag from the pistol but do not eject. (like in "Into the Radius")
    /// The magazine should then be grabbable. Grabbing it allows the player to inspect the number of rounds left in the mag.
    /// </summary>
    private void ReleaseMagazine()
    {
        // Theres no mag to release if no mag is inserted.
        if (!m_HasInsertedMagazine)
        {
            return;
        }

        // Play release mag anim and sfx
        m_Animator.SetTrigger("ReleaseMagazine");
        m_AudioSource.PlayOneShot(m_SFXReleaseMagazine);
    }

    public void SpawnReleasedMagazine()
    {
        Debug.Log("Spawning released mag...");
        // Spawn magazine, initialize its data and set to kinematic
        Magazine releasedMagazine = Instantiate(m_MagazinePrefab, m_MagazineReleasePoint).GetComponent<Magazine>();
        releasedMagazine.ImportData(m_MagazineInfo);
        releasedMagazine.GetComponent<Rigidbody>().isKinematic = true;
        releasedMagazine.transform.localScale = Vector3.one;
        releasedMagazine.CanBeLoaded = false;
        releasedMagazine.WasReleasedFromGun = true;
        releasedMagazine.name = "ReleasedMagazine";

        m_MagazineModel.SetActive(false);
        m_HasInsertedMagazine = false;
    }

    /// <summary>
    /// Spawn the ejected magazine at the mag ejection location in the correct orientation,
    /// then add force in the ejection direction.
    /// </summary>
    /// <param name="context"></param>
    public void EjectMagazine()
    {
        // Theres nothing to eject if theres no magazine loaded.
        if (!m_HasInsertedMagazine)
        {
            return;
        }

        // Play release mag sfx
        m_AudioSource.PlayOneShot(m_SFXReleaseMagazine);

        // Spawn magazine, initialize its data and add force
        Magazine ejectedMagazine = Instantiate(m_MagazinePrefab, m_MagazineEjectionPoint.position, m_MagazineEjectionPoint.rotation).GetComponent<Magazine>();
        ejectedMagazine.ImportData(m_MagazineInfo);
        ejectedMagazine.CanBeLoaded = false;
        ejectedMagazine.GetComponent<Rigidbody>().AddForce(m_MagazineEjectionDirection.forward * m_MagazineEjectionPower);
        Physics.IgnoreCollision(ejectedMagazine.GetComponent<MeshCollider>(), GetComponentInChildren<BoxCollider>());
        // After x seconds, enable collision between grip collider and ejectedMag collider (or on trigger exit?)

        // Update this pistol to reflect the changes
        m_HasInsertedMagazine = false;
        m_MagazineModel.SetActive(false);
    }

    private void InsertMagazine()
    {
        Debug.Log($"Inserted mag with {m_MagazineInfo.HeldRounds} bullets.");
        m_MagazineModel.SetActive(true);
        m_AudioSource.PlayOneShot(m_SFXInsertMagazine);
        m_HasInsertedMagazine = true;
    }

    /// <summary>
    /// Call this once the released magazine is grabbed.
    /// </summary>
    public void HideMagazineModel()
    {
        m_MagazineModel.SetActive(false);
    }

    public void AttemptChamberBullet()
    {
        if (m_HasInsertedMagazine && m_MagazineInfo.HeldRounds > 0)
        {
            --m_MagazineInfo.HeldRounds;
            m_HasRoundInChamber = true;
        }

        Debug.Log($"AttemptChamberBullet(): Set animator param [HasRoundInChamber] to {m_HasRoundInChamber}...");
        m_Animator.SetBool("HasRoundInChamber", m_HasRoundInChamber);
    }
}
