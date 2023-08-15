using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class MyDirectInteractor : XRDirectInteractor
{
    public override bool CanSelect(IXRSelectInteractable interactable)
    {
        if (interactable as Pistol != null && interactable.transform.GetComponentInParent<Pistol>().isSelected)
        {
            return false;
        }
        else
        {
            return base.CanSelect(interactable) && (!hasSelection || IsSelecting(interactable));
        }
    }
}
