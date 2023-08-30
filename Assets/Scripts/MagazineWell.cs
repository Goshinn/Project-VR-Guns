using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Magazine;

public class MagazineWell : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float m_MagazineEjectionPower = 10f;

    [Header("Model References")]
    [SerializeField] private GameObject m_MagazineModel;        // The visual gameobject used for animating mag release and insertion
    [SerializeField] private Collider m_GunGripCollider;

    [Header("Prefab References")]
    [SerializeField] private GameObject m_MagazinePrefab;       // The mag prefab to spawn when releasing/ejecting magazines

    [Header("Location References")]
    [SerializeField] private Transform m_MagazineReleasePoint;
    [SerializeField] private Transform m_MagazineEjectionPoint;
    [SerializeField] private Transform m_MagazineEjectionDirection;

    [Header("SFX")]
    [SerializeField] private AudioClip m_SFXInsertMagazine;
    [SerializeField] private AudioClip m_SFXReleaseMagazine;

    [Header("Private References")]
    private Animator m_Animator;
    private AudioSource m_AudioSource;

    [Header("Members")]
    private bool m_HasInsertedMagazine;
    private bool m_IsLoadingMagazine;
    private MagazineInfo m_MagazineInfo;

    // Getters
    public bool HasInsertedMagazine { get { return m_HasInsertedMagazine; } }
    public MagazineInfo MagazineInfo { get { return m_MagazineInfo; } }


    private void Awake()
    {
        m_Animator = GetComponentInParent<Animator>();
        m_AudioSource = GetComponentInParent<AudioSource>();

        if (m_Animator == null)
        {
            Debug.LogError($"No animator found in parent of {name}");
        }
        if (m_AudioSource == null)
        {
            Debug.LogError($"No audio source found in parent of {name}");
        }
    }

    private void Start()
    {
        m_MagazineModel.SetActive(m_HasInsertedMagazine);
    }

    private void OnTriggerEnter(Collider other)
    {
        // If there is no loaded magazine and a mag is detected, load it into the pistol.
        if (!m_HasInsertedMagazine && !m_IsLoadingMagazine)
        {
            Magazine detectedMagazine = other.GetComponent<Magazine>();
            if (detectedMagazine != null && !detectedMagazine.WasReleasedFromGun)
            {
                Debug.Log("MagWell: OnTriggerEnter() - Detected magazine. Triggering load mag anim...");
                m_MagazineInfo = detectedMagazine.GetMagazineInfo();

                // Play load magazine animation (which plays load mag sfx via anim event)
                m_Animator.SetTrigger("LoadMagazine");
                m_MagazineModel.SetActive(true);
                Destroy(other.gameObject);
                m_IsLoadingMagazine = true;
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
                Debug.Log($"MagWell: OnTriggerExit() - Setting mag was released from gun...");
                detectedMagazine.WasReleasedFromGun = false;

                foreach (Collider collider in detectedMagazine.colliders)
                {
                    Physics.IgnoreCollision(collider, m_GunGripCollider, false);
                }
            }
        }
    }

    /// <summary>
    /// Only usable if there is a mag inserted into the magazine well.
    /// Release the mag from the pistol but do not eject. (like in "Into the Radius")
    /// The magazine should then be grabbable. Grabbing it allows the player to inspect the number of rounds left in the mag.
    /// </summary>
    public void AttemptReleaseMagazine()
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

    /// <summary>
    /// Spawn a grabbable magazine at the mag release point.
    /// </summary>
    public void SpawnReleasedMagazine()
    {
        Debug.Log("Spawning released mag...");
        // Spawn magazine, initialize its data and set to kinematic
        Magazine releasedMagazine = Instantiate(m_MagazinePrefab, m_MagazineReleasePoint).GetComponent<Magazine>();
        releasedMagazine.ImportData(m_MagazineInfo);
        releasedMagazine.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
        releasedMagazine.transform.localScale = Vector3.one;
        releasedMagazine.WasReleasedFromGun = true;
        releasedMagazine.name = "ReleasedMagazine";
        foreach (Collider collider in releasedMagazine.colliders)
        {
            Physics.IgnoreCollision(collider, m_GunGripCollider, true);
        }

        m_MagazineModel.SetActive(false);
        m_HasInsertedMagazine = false;
    }

    /// <summary>
    /// Spawn the ejected magazine at the mag ejection location in the correct orientation,
    /// then add force in the ejection direction.
    /// </summary>
    /// <param name="context"></param>
    public void AttemptEjectMagazine()
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
        ejectedMagazine.WasReleasedFromGun = true;
        ejectedMagazine.GetComponent<Rigidbody>().AddForce(m_MagazineEjectionDirection.forward * m_MagazineEjectionPower);
        Physics.IgnoreCollision(ejectedMagazine.GetComponent<MeshCollider>(), GetComponentInChildren<BoxCollider>());
        // After x seconds, enable collision between grip collider and ejectedMag collider (or on trigger exit?)

        // Update this pistol to reflect the changes
        m_HasInsertedMagazine = false;
        m_MagazineModel.SetActive(false);
    }

    public void InsertMagazine()
    {
        Debug.Log($"Inserted mag with {m_MagazineInfo.HeldRounds} bullets.");
        m_MagazineModel.SetActive(true);
        m_AudioSource.PlayOneShot(m_SFXInsertMagazine);
        m_HasInsertedMagazine = true;
        m_IsLoadingMagazine = false;
    }

    public void DeductRoundFromMagazine()
    {
        --m_MagazineInfo.HeldRounds;
    }

    public void HideMagazineModel()
    {
        m_MagazineModel.SetActive(false);
    }
}
