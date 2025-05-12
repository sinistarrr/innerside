using StarterAssets;
using UnityEngine;

public class AnimationStateController : MonoBehaviour
{   
    private Animator animator;
    private StarterAssetsInputs playerControls;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        animator = GetComponent<Animator>();
        playerControls = gameObject.AddComponent<StarterAssetsInputs>();
    }

    
}
