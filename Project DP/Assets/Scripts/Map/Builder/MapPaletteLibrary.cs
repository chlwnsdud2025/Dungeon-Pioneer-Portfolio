using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 팔레트 하나의 타입. 타입이 배치 방식(격자 칠하기 / 자유 배치)과 맵 프리팹 안의 묶음을 정합니다.
///
/// ★ 순서를 바꾸거나 중간에 끼워 넣지 마세요. 팔레트 파일(MapPaletteLibrary.asset)에 번호로 저장되어,
///   순서가 바뀌면 기존 팔레트의 타입이 전부 밀립니다. 새 타입은 항상 맨 뒤에만 추가합니다.
/// </summary>
public enum MapTileType
{
    Floor,
    Wall,
    Entrance,
    Exit,
    Chest,
    Trap,
    Enemy,
    Pushable,
    Free,       // 자유 배치. 격자와 무관하게 하나씩 놓음
    Decoration  // 개척 장식. 자유 배치와 같지만 개척 완료 시에만 보임
}

/// <summary>팔레트 하나 = 이름 + 타입 + 프리팹 목록. 같은 타입의 팔레트가 여럿이면 같은 묶음에 들어갑니다.</summary>
[Serializable]
public class MapPalette
{
    public string name = "새 팔레트";
    public MapTileType type = MapTileType.Floor;
    public List<GameObject> prefabs = new List<GameObject>();
}

/// <summary>
/// 맵빌더 팔레트 전체. 프로젝트에 이 파일 하나만 두고 던전·필드·마을 팔레트를 함께 담습니다.
///
/// 팔레트를 MapBuilder(씬)에 두지 않는 이유: MapBuildScene에는 작업 중인 맵 사본이 함께 저장되어
/// 커밋하지 않는 것이 원칙인데, 팔레트는 팀이 공유해야 합니다. 그래서 씬과 분리된 파일에 둡니다.
///
/// 편집은 MapBuilder 인스펙터에서만 합니다. 이 파일을 직접 열 일은 없습니다.
/// MapBuildScene만 이 파일을 참조하고 그 씬은 빌드 목록에 없으므로 게임 빌드에는 포함되지 않습니다.
/// </summary>
public class MapPaletteLibrary : ScriptableObject
{
    public List<MapPalette> dungeon = new List<MapPalette>();
    public List<MapPalette> field = new List<MapPalette>();
    public List<MapPalette> town = new List<MapPalette>();
}
