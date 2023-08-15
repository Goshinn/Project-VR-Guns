using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using static UnityEngine.Rendering.DebugUI;

public class Magazine : XRGrabInteractable
{
    [Header("Info")]
    [SerializeField] private int m_MagazineSize;
    [SerializeField] private int m_HeldRounds;

    [Header("Model References")]
    [SerializeField] private GameObject m_TopMostBullet;

    [Serializable]
    public struct MagazineInfo
    {
        public int MagazineSize;
        public int HeldRounds;
    }

    // Private members
    public bool CanBeLoaded = true; // Set this to true upon grabbing the mag. It is only set to false when ejecting and releasing the mag from a gun.
    public bool WasReleasedFromGun = false;

    public int HeldRounds
    {
        get { return m_HeldRounds; }
        set
        {
            if (value >= 0)
            {
                m_HeldRounds = value;
            }
            else
            {
                Debug.LogError("You are attempting to set magazine.m_HeldRounds to be < 0!");
            }
        }
    }

    public MagazineInfo GetMagazineInfo()
    {
        MagazineInfo magInfo = new MagazineInfo();
        magInfo.MagazineSize = m_MagazineSize;
        magInfo.HeldRounds = m_HeldRounds;
        return magInfo;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        m_TopMostBullet.SetActive(m_HeldRounds > 0);
    }

    /// <summary>
    /// Upon grabbing a magazine, if it was released from a gun, ensure that its rigidbody is set to non kinematic and it is unparented.
    /// </summary>
    /// <param name="args"></param>
    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        base.OnSelectEntered(args);

        // Currently, there is no need to check if the mag was released from a gun or not as these settings should be universal for all mags upon being grabbed.
        GetComponent<Rigidbody>().isKinematic = false;
        transform.parent = null;    // Unparent from GunMagazineReleasePoint
        CanBeLoaded = true;
    }

    public void ImportData(MagazineInfo magazineInfo)
    {
        m_HeldRounds = magazineInfo.HeldRounds;
        m_MagazineSize = magazineInfo.MagazineSize;
        m_TopMostBullet.SetActive(m_HeldRounds > 0);
    }
}
