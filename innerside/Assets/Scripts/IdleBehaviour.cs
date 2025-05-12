using NUnit.Framework;
using UnityEngine;

public class IdleBehaviour : StateMachineBehaviour
{
    [field: SerializeField] private float TimeUntilIdle { get; set; }
    [field: SerializeField] private int NumberOfIdleAnimations { get; set; }

    private bool IsIdle { get; set; }
    private float IdleTime { get; set; }
    private int IdleAnimation { get; set; }
    // OnStateEnter is called when a transition starts and the state machine starts to evaluate this state
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        ResetIdle();
    }

    // OnStateUpdate is called on each Update frame between OnStateEnter and OnStateExit callbacks
    override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (!IsIdle)
        {
            IdleTime += Time.deltaTime;

            if(IdleTime > TimeUntilIdle && stateInfo.normalizedTime % 1 < 0.02f){
                IsIdle = true;
                IdleAnimation = Random.Range(1, NumberOfIdleAnimations + 1);
                IdleAnimation = IdleAnimation * 2 - 1;
                
                animator.SetFloat("IdleAnimation", IdleAnimation - 1);
            }
        }
        else if(stateInfo.normalizedTime % 1 > 0.98f){
            ResetIdle();
        }

        animator.SetFloat("IdleAnimation", IdleAnimation, 0.2f, Time.deltaTime);
    }

    private void ResetIdle(){
        if(IsIdle){
            IdleAnimation--;
        }
        IsIdle = false;
        IdleTime = 0;
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
