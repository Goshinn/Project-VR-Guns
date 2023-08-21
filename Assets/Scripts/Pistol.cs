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
    [SerializeField] private Vector3 m_CartridgeEjectionRotationOffset = new Vector3(90f, 0, 0);

    [Header("Component References")]
    [SerializeField] private MagazineWell m_MagazineWell;

    [Header("Location References")]
    [SerializeField] private Transform m_BulletSpawnPoint;
    [SerializeField] private Transform m_CartridgeEjectionPoint;

    [Header("Prefab References")]
    [SerializeField] private GameObject m_CartridgePrefab;              // Prefab of the round with bullet intact. (not round casing)

    [Header("Particle System References")]
    [SerializeField] private ParticleSystem m_MuzzleFlashVFX;
    [SerializeField] private ParticleSystem m_CartridgeEjectionVFX;

    [Header("SFX")]
    [SerializeField] private AudioClip m_SFXGunShot;
    [SerializeField] private AudioClip m_SFXEmptyClick;

    // Private component references
    private Animator m_Animator;
    private AudioSource m_AudioSource;
    private MyActionBasedController m_Controller;

    // Private members
    private bool m_HasRoundInChamber;

    public bool HasRoundInChamber { get { return m_HasRoundInChamber; } }

    override protected void Awake()
    {
        base.Awake();
        m_Animator = GetComponent<Animator>();
        m_AudioSource = GetComponent<AudioSource>();
    }

    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        base.OnSelectEntered(args);
        XRBaseControllerInteractor controllerInteractor = args.interactorObject as XRBaseControllerInteractor;
        m_Controller = controllerInteractor.xrController as MyActionBasedController;

        if (m_Controller != null)
        {
            // Subscribing to input events...
            m_Controller.primaryButtonTap.action.performed += context => AttemptEjectMagazine();
            m_Controller.primaryButtonLongPress.action.performed += context => AttemptReleaseMagazine();
        }
    }

    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        base.OnSelectExited(args);

        if (m_Controller != null)
        {
            // Unsubscribing to input events...
            m_Controller.primaryButtonTap.action.performed -= context => AttemptEjectMagazine();
            m_Controller.primaryButtonLongPress.action.performed -= context => AttemptReleaseMagazine();
        }
    }

    public void PullTrigger(ActivateEventArgs args)
    {
        if (m_HasRoundInChamber)
        {
            // If the magazine still has rounds, chamber it
            if (m_MagazineWell.HasInsertedMagazine && m_MagazineWell.MagazineInfo.HeldRounds > 0)
            {
                m_MagazineWell.DeductRoundFromMagazine();
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
                //m_MuzzleFlashVFX.Emit((int)m_MuzzleFlashVFX.emission.GetBurst(0).count.constant);
                m_MuzzleFlashVFX.Stop();
                m_MuzzleFlashVFX.Play();
            }
            m_Controller.SendHapticImpulse(1f, 0.1f);

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
    public void ClearRoundInChamber()
    {
        m_HasRoundInChamber = false;

        // Cancels function if ejection slot hasn't been set or there's no casing
        if (!m_CartridgePrefab || !m_CartridgeEjectionPoint)
        {
            Debug.LogWarning("No shell casing prefab or shell casing ejection point assigned.");
            return;
        }

        // Create the cartridge
        GameObject ejectedCartridge = Instantiate<GameObject>(m_CartridgePrefab, m_CartridgeEjectionPoint.position, m_CartridgeEjectionPoint.rotation);
        ejectedCartridge.transform.Rotate(m_CartridgeEjectionRotationOffset);  // By right, all models stand upright, including shell casings. To orient correctly, apply the rotation offset.
        // Add force on casing to push it out
        Rigidbody bulletRB = ejectedCartridge.GetComponent<Rigidbody>();
        bulletRB.AddExplosionForce(UnityEngine.Random.Range(m_CartridgeEjectionPower * 0.7f, m_CartridgeEjectionPower),
                                        (m_CartridgeEjectionPoint.position - m_CartridgeEjectionPoint.right * 0.3f - m_CartridgeEjectionPoint.up * 0.6f),
                                        1f);
        // Add torque to make casing spin in random direction
        bulletRB.AddTorque(new Vector3(0, UnityEngine.Random.Range(100f, 500f), UnityEngine.Random.Range(100f, 1000f)), ForceMode.Impulse);

        // Destroy casing after X seconds
        Destroy(ejectedCartridge, 60f);
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

    public void AttemptChamberBullet()
    {
        if (m_MagazineWell.HasInsertedMagazine && m_MagazineWell.MagazineInfo.HeldRounds > 0)
        {
            m_MagazineWell.DeductRoundFromMagazine();
            m_HasRoundInChamber = true;
        }

        Debug.Log($"AttemptChamberBullet(): Set animator param [HasRoundInChamber] to {m_HasRoundInChamber}...");
        m_Animator.SetBool("HasRoundInChamber", m_HasRoundInChamber);
    }


    /* -------------------------- WRAPPER FUNCTIONS -------------------------- */
    // Binded to primary button long press
    public void AttemptReleaseMagazine() { m_MagazineWell.AttemptReleaseMagazine(); }

    // Binded to primary button tap
    public void AttemptEjectMagazine() { m_MagazineWell.AttemptEjectMagazine(); }

    // Called via anim evnt when magazine has been fully released.
    public void SpawnReleasedMagazine() { m_MagazineWell.SpawnReleasedMagazine(); }

    // Called via anim evnt when mag has been fully inserted.
    public void InsertMagazine() { m_MagazineWell.InsertMagazine(); }
}
