using System.Collections.Generic;
using UnityEngine;
using System;

public class EnemyStateMachine
{
    public EnemyBaseState CurrentState { get; private set; }

    // 상태의 타입을 키로 삼아 저장
    private Dictionary<Type, EnemyBaseState> stateDictionary = new Dictionary<Type, EnemyBaseState>();

    public void AddState(EnemyBaseState state)
    {
        stateDictionary.TryAdd(state.GetType(), state);
    }

    public void Initialize<T>() where T : EnemyBaseState
    {
        if (stateDictionary.TryGetValue(typeof(T), out EnemyBaseState startingState))
        {
            CurrentState = startingState;
            CurrentState.Enter();
        }
    }

    public void ChangeState<T>() where T : EnemyBaseState
    {
        if (CurrentState != null)
        {
            CurrentState.Exit();
        }

        CurrentState = stateDictionary[typeof(T)];
        CurrentState.Enter();
    }

    public void Update()
    {
        CurrentState?.Update();
    }

    public void FixedUpdate()
    {
        CurrentState?.FixedUpdate();
    }
}