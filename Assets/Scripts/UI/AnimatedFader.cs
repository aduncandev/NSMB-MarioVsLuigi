using System;
using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using NSMB;
using Quantum;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.UI;

public class AnimatedFader : MonoBehaviour {
    private static readonly int ParamDirection = Animator.StringToHash("direction");
    private static readonly int ParamCircle = Animator.StringToHash("circle");
    private static readonly int ParamDissolve = Animator.StringToHash("dissolve");
    private static readonly int ParamRespawn = Animator.StringToHash("respawn");

    public enum FadeStyle {
        Cut,
        Dissolve,
        Circle,
        Respawn,    // bowser shape on IN, star shape on OUT
    }

    [SerializeField] private Sprite defaultRespawnStyleSilhouette;
    
    [SerializeField] private Animator anim;
    [SerializeField] private Image shapeImage;
    private FadeStyle currentInStyle, currentOutStyle;

    public void Fade(FadeStyle inStyle, FadeStyle outStyle = FadeStyle.Cut, Action onInComplete = null) {
        currentInStyle = inStyle;
        currentOutStyle = outStyle;
        
        anim.SetBool(ParamDirection, true);
        // used a switch instead of mapping enum to anim name directly in case there are fade styles that have additional logic
        switch (inStyle) {
        case FadeStyle.Circle:
            anim.SetTrigger(ParamCircle);
            break;
        case FadeStyle.Dissolve:
            anim.SetTrigger(ParamDissolve);
            break;
        case FadeStyle.Respawn:
            anim.SetTrigger(ParamRespawn);
            break;
        case FadeStyle.Cut:
            break;
        }
        StartCoroutine(WaitForAnimation(onInComplete));
    }
    
    private IEnumerator WaitForAnimation(Action onComplete) {
        yield return null;
        yield return new WaitUntil(() => anim.GetCurrentAnimatorStateInfo(0).normalizedTime > 1 && !anim.IsInTransition(0));
        yield return new WaitForSeconds(0.4f);
        onComplete?.Invoke();
        yield return new WaitForSeconds(0.1f);
        FadeOut();
    }

    private void FadeOut() {
        anim.SetBool(ParamDirection, false);
        // ditto.
        switch (currentOutStyle) {
        case FadeStyle.Circle:
            anim.SetTrigger(ParamCircle);
            break;
        case FadeStyle.Dissolve:
            anim.SetTrigger(ParamDissolve);
            break;
        case FadeStyle.Respawn:
            anim.SetTrigger(ParamRespawn);
            break;
        case FadeStyle.Cut:
            break;
        }
    }

    public void SetRespawnStyleSilhouetteSprite([CanBeNull] Sprite silhouette)
    {
        shapeImage.sprite = silhouette ?? defaultRespawnStyleSilhouette;
    }
}
