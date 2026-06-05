using System.Collections.Generic;
using UnityEngine;

namespace KRTD.Map
{
    /// <summary>
    /// 건물 한 종류의 정적 데이터.
    /// BuildSpot에 설치되면 buildingPrefab 이 인스턴스화된다.
    /// 시각, Animator, 스크립트(공격 로직 등)는 모두 prefab 안에 포함시킨다.
    /// </summary>
    [CreateAssetMenu(fileName = "Building_New", menuName = "KRTD/Building Data")]
    public class BuildingData : ScriptableObject
    {
        [Header("표시 정보")]
        public string buildingName = "Building";

        [Tooltip("건설 메뉴(UI)용 작은 아이콘")]
        public Sprite icon;

        [Header("실제 건물")]
        [Tooltip("BuildSpot에 설치될 때 인스턴스화되는 프리팹. 시각/애니메이션/공격 로직 모두 이 안에 둔다.")]
        public GameObject buildingPrefab;

        [Header("기타")]
        [Tooltip("건설 비용 (자원 시스템 도입 후 사용)")]
        public int cost = 0;

        [Header("업그레이드")]
        [Tooltip("이 건물의 다음 단계 데이터 (단일 진화). 비어있으면 더 이상 업그레이드 불가. " +
            "nextBranches 가 1개 이상이면 그쪽이 우선되어 분기 진화 UI 가 표시된다.")]
        public BuildingData nextUpgrade;

        [Tooltip("분기 진화 후보. 1개 이상이면 관리 메뉴에 각 후보가 별도 슬롯으로 나타나 플레이어가 선택. " +
            "비어있거나 0개면 nextUpgrade(단일) 로 폴백.")]
        public BuildingData[] nextBranches;

        /// <summary>분기 진화 후보가 1개 이상 정의돼 있으면 true.</summary>
        public bool HasBranches
        {
            get
            {
                if (nextBranches == null) return false;
                for (int i = 0; i < nextBranches.Length; i++)
                    if (nextBranches[i] != null) return true;
                return false;
            }
        }

        /// <summary>업그레이드 가능 여부 — 분기 또는 단일 다음 단계가 있으면 true.</summary>
        public bool CanUpgrade => HasBranches || nextUpgrade != null;

        /// <summary>
        /// 다음 단계 후보들 (UI 가 순회하며 슬롯 생성).
        /// nextBranches 가 1개 이상이면 그쪽을 사용, 아니면 nextUpgrade(단일).
        /// 둘 다 비어있으면 빈 시퀀스.
        /// </summary>
        public IEnumerable<BuildingData> NextOptions
        {
            get
            {
                if (HasBranches)
                {
                    for (int i = 0; i < nextBranches.Length; i++)
                        if (nextBranches[i] != null) yield return nextBranches[i];
                    yield break;
                }
                if (nextUpgrade != null) yield return nextUpgrade;
            }
        }
    }
}
