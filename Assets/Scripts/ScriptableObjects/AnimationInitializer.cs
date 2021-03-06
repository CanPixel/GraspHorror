﻿using UnityEngine;

[CreateAssetMenu(menuName = "Animation Initializer")]
public class AnimationInitializer : ScriptableObject
{
    public string userClipName = "Animation Clip";
    public string targetClipName = "Animation Clip";
    public TransformSet transform;

    /// <summary>
    /// Initialize an animation with two separate animated Game Objects.
    /// </summary>
    public void Activate(Transform user, Transform target)
    {
        //Initialize
        target.GetComponent<Rigidbody>().isKinematic = true;
        user.GetComponent<Rigidbody>().isKinematic = true;
        target.GetComponent<Collider>().isTrigger = true;
        PlayerController pc = target.GetComponent<PlayerController>();
        pc.ikWeight = 0;
        pc.desiredIKWeight = 0;
        pc.dead = true;
        pc.animator.WriteDefaultValues();

        //Set right transforms
        target.SetParent(user);
        Vector3 localPosition = user.LocalPosition(transform.offsetPosition);
        target.position = localPosition;
        target.eulerAngles = user.eulerAngles + transform.offsetEulerAngles;
        target.localScale += transform.offsetScale;

        //Animating
        Animator ua = user.GetComponent<Animator>();
        ua.ResetTrigger("Attack");
        ua.ResetTrigger("Hurt");
        ua.Play(userClipName, 0, 0);
        target.GetComponent<Animator>().Play(targetClipName);
    }
}

[System.Serializable]
public struct TransformSet
{
    public Vector3 offsetPosition;
    public Vector3 offsetEulerAngles;
    public Vector3 offsetScale;
}
