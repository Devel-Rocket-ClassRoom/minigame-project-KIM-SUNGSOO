using System;
using System.Collections.Generic;
using UnityEngine;

namespace KRTD.Cloud
{
    /// <summary>
    /// 스테이지 한 개의 클리어 기록. 별점은 0~3.
    /// </summary>
    [Serializable]
    public class StageRecord
    {
        public int stageId;
        public bool cleared;
        [Range(0, 3)] public int stars;
    }

    /// <summary>
    /// 계정별로 클라우드(Realtime Database)에 저장되는 플레이어 영구 데이터.
    ///
    /// JsonUtility 로 직렬화해 RTDB <c>users/{uid}</c> 경로에 통째로 저장한다.
    /// JsonUtility 는 Dictionary 를 지원하지 않으므로 스테이지 기록은 List 로 관리하고,
    /// 조회/갱신은 <see cref="GetStage"/> / <see cref="SetStageResult"/> 헬퍼로 한다.
    ///
    /// 저장 항목(사용자 선택):
    ///   - 프로필: 닉네임, 가입 시각
    ///   - 스테이지 진행/별점
    /// </summary>
    [Serializable]
    public class PlayerData
    {
        // --- 프로필 ---
        public string nickname = "";
        /// <summary>가입(최초 저장) 시각 — Unix epoch milliseconds. UTC 기준.</summary>
        public long createdAtUnixMs;
        /// <summary>마지막 저장 시각 — Unix epoch milliseconds. UTC 기준.</summary>
        public long updatedAtUnixMs;

        // --- 스테이지 진행 ---
        public List<StageRecord> stages = new List<StageRecord>();

        /// <summary>클리어한 스테이지 중 가장 높은 stageId. 없으면 0.</summary>
        public int HighestClearedStage
        {
            get
            {
                int max = 0;
                foreach (var s in stages)
                    if (s.cleared && s.stageId > max) max = s.stageId;
                return max;
            }
        }

        /// <summary>모든 스테이지 별점 합계.</summary>
        public int TotalStars
        {
            get
            {
                int sum = 0;
                foreach (var s in stages) sum += s.stars;
                return sum;
            }
        }

        /// <summary>해당 스테이지 기록 조회. 없으면 null.</summary>
        public StageRecord GetStage(int stageId)
        {
            foreach (var s in stages)
                if (s.stageId == stageId) return s;
            return null;
        }

        /// <summary>
        /// 스테이지 클리어 결과 반영. 이미 기록이 있으면 별점이 더 높을 때만 갱신(하향 방지).
        /// </summary>
        public void SetStageResult(int stageId, int stars)
        {
            stars = Mathf.Clamp(stars, 0, 3);
            StageRecord rec = GetStage(stageId);
            if (rec == null)
            {
                stages.Add(new StageRecord { stageId = stageId, cleared = true, stars = stars });
            }
            else
            {
                rec.cleared = true;
                if (stars > rec.stars) rec.stars = stars;
            }
        }
    }
}
