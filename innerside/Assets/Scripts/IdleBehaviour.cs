using NUnit.Framework;
using UnityEngine;

public class IdleBehaviour : StateMachineBehaviour
{
    [field: SerializeField] private float TimeUntilIdle { get; set; }
    [field: SerializeField] private int NumberOfIdleAnimations { get; set; }

    private bool IsIdle { get; set; }
    private float IdleTime { get; set; }
    // OnStateEnter is called when a transition starts and the state machine starts to evaluate this state
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        ResetIdle(animator);
    }

    // OnStateUpdate is called on each Update frame between OnStateEnter and OnStateExit callbacks
    override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (!IsIdle)
        {
            IdleTime += Time.deltaTime;

            if(IdleTime > TimeUntilIdle){
                IsIdle = true;
                int idleAnimation = Random.Range(1, NumberOfIdleAnimations + 1);

                animator.SetFloat("IdleAnimation", idleAnimation);
            }
        }
        else if(stateInfo.normalizedTime % 1 > 0.98){
            ResetIdle(animator);
        }
    }

    private void ResetIdle(Animator animator){
        IsIdle = false;
        IdleTime = 0;

        animator.SetFloat("IdleAnimation", 0);
    }

    // OnStateExit is called when a transition ends and the state machine finishes evaluating this state
    //override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    //{
    //    
    //}

    // OnStateMove is called right after Animator.OnAnimatorMove()
    //override public void OnStateMove(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    //{
    //    // Implement code that processes and affects root motion
    //}

    // OnStateIK is called right after Animator.OnAnimatorIK()
    //override public void OnStateIK(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    //{
    //    // Implement code that sets up animation IK (inverse kinematics)
    //}
}
