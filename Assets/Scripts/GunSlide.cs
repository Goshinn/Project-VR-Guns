using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;

namespace Unity.XRContent.Interaction
{
    /// <summary>
    /// An interactable that follows the position of the interactor on a single axis
    /// </summary>
    public class GunSlide : XRBaseInteractable
    {
        [Serializable]
        public class ValueChangeEvent : UnityEvent<float> { }

        [SerializeField]
        [Tooltip("The object that is visually grabbed and manipulated")]
        Transform m_Slide = null;

        [SerializeField]
        [Tooltip("The rate at which the slide returns back to its original position.")]
        private float m_SlideReturnSpeed = 19f;

        [SerializeField]
        [Tooltip("The value of the slider")]
        [Range(0.0f, 1.0f)]
        float m_Value = 1f;

        [SerializeField]
        [Tooltip("The offset of the slider at value '1'")]
        float m_MaxPosition = 0;

        [SerializeField]
        [Tooltip("The offset of the slider at value '0'")]
        float m_MinPosition = -0.38f;

        [Header("SFX")]
        [SerializeField] private AudioClip m_SFXSlidePull;
        [SerializeField] private AudioClip m_SFXSlideRelease;

        [SerializeField]
        [Tooltip("Events to trigger when the slider is moved")]
        ValueChangeEvent m_OnValueChange = new ValueChangeEvent();

        [Header("Private References")]
        private Pistol m_Pistol;
        IXRSelectInteractor m_Interactor;
        private AudioSource m_AudioSource;

        [Header("Private Members")]
        private bool m_HasEjectedCasing;
        private bool m_HasPulledBackSlide;

        /// <summary>
        /// The value of the slider
        /// </summary>
        public float Value
        {
            get { return m_Value; }
            set
            {
                SetValue(value);
                SetSliderPosition(value);
            }
        }

        /// <summary>
        /// Events to trigger when the slider is moved
        /// </summary>
        public ValueChangeEvent OnValueChange => m_OnValueChange;

        protected override void Awake()
        {
            base.Awake();
            m_AudioSource = GetComponentInParent<AudioSource>();
            if (m_AudioSource == null)
            {
                Debug.LogError($"No audiosource found in parent of {name}.");
            }

            m_Pistol = GetComponentInParent<Pistol>();
        }

        void Start()
        {
            SetValue(m_Value);
            SetSliderPosition(m_Value);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            selectEntered.AddListener(StartGrab);
            selectExited.AddListener(EndGrab);
        }

        protected override void OnDisable()
        {
            selectEntered.RemoveListener(StartGrab);
            selectExited.RemoveListener(EndGrab);
            base.OnDisable();
        }

        void StartGrab(SelectEnterEventArgs args)
        {
            GetComponentInParent<Animator>().enabled = false;
            m_Interactor = args.interactorObject;
            UpdateSliderPosition();
        }

        void EndGrab(SelectExitEventArgs args)
        {
            GetComponentInParent<Animator>().enabled = true;
            m_Interactor = null;
        }

        public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
        {
            base.ProcessInteractable(updatePhase);

            if (updatePhase == XRInteractionUpdateOrder.UpdatePhase.Dynamic)
            {
                if (isSelected)
                {
                    UpdateSliderPosition();
                }
                else if (Value < 1f)
                {
                    SetValue(Mathf.Clamp01(Value + m_SlideReturnSpeed * Time.deltaTime));
                    SetSliderPosition(Value);
                }
            }
        }

        /// <summary>
        /// This should be hooked up to OnValueChanged UnityEvent.
        /// </summary>
        public void HandleSlidePullMechanism()
        {
            // Slide pulled almost all the way back
            if (Value < 0.05f)
            {
                if (!m_HasPulledBackSlide)
                {
                    m_AudioSource.PlayOneShot(m_SFXSlidePull);
                    m_HasPulledBackSlide = true;
                }
                
                if (m_Pistol.HasRoundInChamber && !m_HasEjectedCasing)
                {
                    m_Pistol.EjectBullet();
                    m_HasEjectedCasing = true;
                }
            }
            // Slide almost returned to resting position
            else if (Value > 0.95f)
            {
                if (m_HasPulledBackSlide)
                {
                    m_AudioSource.PlayOneShot(m_SFXSlideRelease);
                    m_HasPulledBackSlide = false;

                    if (!m_Pistol.HasRoundInChamber)
                    {
                        m_Pistol.AttemptChamberBullet();
                    }
                }

                if (m_HasEjectedCasing)
                {
                    m_HasEjectedCasing = false;                 
                }
            }
        }

        void UpdateSliderPosition()
        {
            // Put anchor position into slider space
            var localPosition = transform.InverseTransformPoint(m_Interactor.GetAttachTransform(this).position);
            var sliderValue = Mathf.Clamp01((localPosition.z - m_MinPosition) / (m_MaxPosition - m_MinPosition));
            SetValue(sliderValue);
            SetSliderPosition(sliderValue);
        }

        void SetSliderPosition(float value)
        {
            if (m_Slide == null)
                return;

            var handlePos = m_Slide.localPosition;
            handlePos.z = Mathf.Lerp(m_MinPosition, m_MaxPosition, value);
            m_Slide.localPosition = handlePos;
        }

        void SetValue(float value)
        {
            m_Value = value;
            m_OnValueChange.Invoke(m_Value);
        }

        void OnDrawGizmosSelected()
        {
            var sliderMinPoint = transform.TransformPoint(new Vector3(0.0f, 0.0f, m_MinPosition));
            var sliderMaxPoint = transform.TransformPoint(new Vector3(0.0f, 0.0f, m_MaxPosition));

            Gizmos.color = Color.green;
            Gizmos.DrawLine(sliderMinPoint, sliderMaxPoint);
        }

        void OnValidate()
        {
            SetSliderPosition(m_Value);
        }
    }
}
