using UnityEngine;

public abstract class EnemyBaseState
{
    protected EnemyController enemy;
    protected EnemyStateMachine stateMachine;

    public EnemyBaseState(EnemyController enemy, EnemyStateMachine stateMachine)
    {
        this.enemy = enemy;
        this.stateMachine = stateMachine;
    }

    public virtual void Enter() { }
    public virtual void Update() { }
    public virtual void FixedUpdate() { }
    public virtual void Exit() { }
}