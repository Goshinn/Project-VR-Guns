using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class DummyReleasedMagazine : XRGrabInteractable
{
    [SerializeField] private Magazine m_MagazinePrefab;

    protected override void Grab()
    {
        // Spawn actual grabbable magazine and grab that mag instead.
        Magazine spawnedMag = Instantiate(m_MagazinePrefab, transform.position, transform.rotation);

        // Hide dummy released mag
        gameObject.SetActive(false);
    }
}
