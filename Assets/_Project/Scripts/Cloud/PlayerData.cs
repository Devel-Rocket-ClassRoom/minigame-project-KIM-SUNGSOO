using System;
using System.Collections.Generic;
using UnityEngine;

namespace KRTD.Cloud
{
    [Serializable]
    public class StageRecord
    {
        public int stageId;
        public bool cleared;
        [Range(0, 3)] public int stars;
        public int attempts;    // 승/패 상관없이 한 판 끝낼 때마다 +1
        public int clearCount;  // 승리 횟수
    }

    /// <summary>
    /// 계정별로 RTDB users/{uid} 에 JsonUtility 로 저장되는 영구 데이터.
    /// JsonUtility 가 Dictionary 를 지원하지 않아 스테이지는 List 로 관리한다.
    /// </summary>
    [Serializable]
    public class PlayerData
    {
        public string nickname = "";
        public long createdAtUnixMs;
        public long updatedAtUnixMs;

        [Range(0f, 1f)] public float bgmVolume = 0.8f;
        [Range(0f, 1f)] public float sfxVolume = 0.8f;

        public List<StageRecord> stages = new List<StageRecord>();

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

        public int TotalStars
        {
            get
            {
                int sum = 0;
                foreach (var s in stages) sum += s.stars;
                return sum;
            }
        }

        public int TotalAttempts
        {
            get
            {
                int sum = 0;
                foreach (var s in stages) sum += s.attempts;
                return sum;
            }
        }

        public StageRecord GetStage(int stageId)
        {
            foreach (var s in stages)
                if (s.stageId == stageId) return s;
            return null;
        }

        public StageRecord GetOrCreateStage(int stageId)
        {
            StageRecord rec = GetStage(stageId);
            if (rec == null)
            {
                rec = new StageRecord { stageId = stageId };
                stages.Add(rec);
            }
            return rec;
        }

        public void RecordStageOutcome(int stageId, bool won, int stars)
        {
            StageRecord rec = GetOrCreateStage(stageId);
            rec.attempts++;
            if (won)
            {
                rec.cleared = true;
                rec.clearCount++;
                stars = Mathf.Clamp(stars, 0, 3);
                if (stars > rec.stars) rec.stars = stars; // 별점은 더 높을 때만 갱신(하향 방지)
            }
        }
    }
}
