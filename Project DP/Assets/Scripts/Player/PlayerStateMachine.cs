using System.Collections.Generic;
using UnityEngine;
using System;


public class PlayerStateMachine
{
    public PlayerBaseState CurrentState { get; private set; }

    // 상태의 '타입(Type)'을 키로 삼아 상태 인스턴스를 저장하는 딕셔너리
    private Dictionary<Type, PlayerBaseState> stateDictionary = new Dictionary<Type, PlayerBaseState>();

    // 초기화 시 생성된 상태들을 딕셔너리에 등록
    public void AddState(PlayerBaseState state)
    {
        stateDictionary.TryAdd(state.GetType(), state);
    }

    // 게임 시작 시 최초 상태 설정
    public void Initialize<T>() where T : PlayerBaseState
    {
        if (stateDictionary.TryGetValue(typeof(T), out PlayerBaseState startingState))
        {
            CurrentState = startingState;
            CurrentState.Enter();
        }
    }

    // 제네릭을 활용한 상태 전환 (예: stateMachine.ChangeState<WalkState>(); )
    public void ChangeState<T>() where T : PlayerBaseState
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
