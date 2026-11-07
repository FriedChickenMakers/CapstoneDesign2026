using System;
using System.Globalization;
using System.Linq;
using CapstoneDesign.Plants;
using CapstoneDesign.Runtime.LocalState;

namespace CapstoneDesign.Runtime
{
    // Read-only album projection. Membership comes from individual identity, never a date-range guess.
    public static class PlantAlbumHistory
    {
        public static PlantMemoryLeaf[] Leaves(GardenState state, string plantId) =>
            state?.PlantCollection?.Plants.FirstOrDefault(p => p.Id == plantId)?.Memories
                .Select(m => new PlantMemoryLeaf { OrganId = m.OrganId, Slot = m.SlotIndex,
                    Linked = m.Kind != MemoryRecordKind.None }).ToArray() ?? Array.Empty<PlantMemoryLeaf>();

        public static MemoryOrganState Memory(GardenState state, string plantId, string id) =>
            state?.PlantCollection?.Plants.FirstOrDefault(p => p.Id == plantId)?.Memories
                .FirstOrDefault(m => m.Kind != MemoryRecordKind.None && (m.OrganId == id || m.RecordId == id));

        public static GardenPlantMemo MemoryRecord(GardenState state, string plantId, string id)
        {
            var plant = state?.PlantCollection?.Plants.FirstOrDefault(p => p.Id == plantId);
            if (plant == null) return null;
            var memory = Memory(state, plantId, id);
            if (memory == null) return null;
            if (memory.Kind == MemoryRecordKind.DailyEmotion)
                return plant.Memos.FirstOrDefault(m => m.Id == memory.RecordId && m.PlantInstanceId == plantId)?.Copy();
            var memo = new GardenPlantMemo { Id = memory.OrganId, PlantInstanceId = plantId,
                CreatedUtc = memory.CreatedUtc, DailyEmotionRecordId = "", Mood = "", Note = "" };
            if (memory.Kind == MemoryRecordKind.SessionReflection)
            {
                var session = state.Sessions.FirstOrDefault(s => s.SessionId == memory.RecordId);
                if (session == null) return null;
                memo.Mood = session.Mood ?? ""; memo.Note = session.Note ?? "";
                if (FocusMissionPolicy.RoleFor(session) == FocusMissionCatalog.BasicRole)
                { memo.BasicSessionId = session.SessionId; memo.BasicMissionId = session.MissionId; memo.BasicMissionTitle = session.MissionTitleSnapshot ?? ""; }
                else { memo.AdditionalSessionId = session.SessionId; memo.AdditionalMissionId = session.MissionId; memo.AdditionalMissionTitle = session.MissionTitleSnapshot ?? ""; }
            }
            else if (memory.Kind == MemoryRecordKind.Experience)
            {
                var record = state.Experiences.FirstOrDefault(e => e.Id == memory.RecordId);
                if (record == null) return null;
                memo.Mood = string.Join(" · ", new[] { record.Type, record.Emotion }.Where(s => !string.IsNullOrWhiteSpace(s)));
                memo.Note = string.Join("\n\n", new[] { record.Event,
                    string.IsNullOrWhiteSpace(record.Body) ? null : "몸의 느낌 · " + record.Body,
                    string.IsNullOrWhiteSpace(record.Thought) ? null : "떠오른 생각 · " + record.Thought }.Where(s => !string.IsNullOrWhiteSpace(s)));
            }
            return memo;
        }
        static string Day(string value)
        {
            return DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
                ? day.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture) : null;
        }
        static string LocalDay(string utc, TimeZoneInfo zone)
        {
            return DateTimeOffset.TryParse(utc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var instant)
                ? TimeZoneInfo.ConvertTime(instant, zone ?? TimeZoneInfo.Utc).ToString("yyyy.MM.dd", CultureInfo.InvariantCulture) : null;
        }
        public static string Period(GardenPlantPhoto photo, TimeZoneInfo zone)
        {
            if (photo == null) return "기간 기록 없음";
            string start = Day(photo.StartedCycleId);
            string end = Day(photo.SavedCycleId) ?? LocalDay(photo.SavedUtc, zone) ?? "보관일 기록 없음";
            return (start ?? "시작일 기록 없음") + " ~ " + end;
        }
        public static string Date(GardenPlantMemo memo, TimeZoneInfo zone)
        {
            if (memo == null) return "날짜 기록 없음";
            string id = memo.DailyEmotionRecordId;
            if (id != null && id.StartsWith(DailyEmotionPolicy.RecordPrefix, StringComparison.Ordinal))
            {
                string date = Day(id.Substring(DailyEmotionPolicy.RecordPrefix.Length));
                if (date != null) return date;
            }
            return LocalDay(memo.CreatedUtc, zone) ?? "날짜 기록 없음";
        }
        public static string MissionLine(GardenPlantMemo memo, bool basic)
        {
            string id = basic ? memo.BasicMissionId : memo.AdditionalMissionId;
            string title = basic ? memo.BasicMissionTitle : memo.AdditionalMissionTitle;
            string label = basic ? "기본" : "추가";
            if (string.IsNullOrEmpty(id) && string.IsNullOrEmpty(title)) return label + " · 기록 없음";
            string code = string.IsNullOrEmpty(id) ? "" : id.Substring(id.LastIndexOf(':') + 1);
            return label + " · " + (code.Length == 0 ? "" : code + " ") + (string.IsNullOrEmpty(title) ? "미션명 기록 없음" : title);
        }
        public static GardenPlantMemo[] Records(GardenState state, GardenPlantPhoto photo)
        {
            if (state == null || photo == null) return Array.Empty<GardenPlantMemo>();
            var plant = state.PlantCollection?.Plants.FirstOrDefault(p => p.Id == photo.SourcePlantInstanceId);
            var records = plant == null ? photo.Memos.Where(memo => memo.PlantInstanceId == photo.SourcePlantInstanceId)
                .Select(memo => memo.Copy()).ToList() : plant.Memories.Where(m => m.Kind != MemoryRecordKind.None)
                .Select(m => MemoryRecord(state, plant.Id, m.OrganId)).Where(m => m != null).ToList();
            // Earlier daily rewards grew the plant and had no 3D paper. Show them
            // only while the original record and exact individual growth receipt exist.
            foreach (var daily in state.DailyEmotionRecords.Where(record => record.RewardVersion == 0 && record.IsCompleted))
            {
                if (records.Any(entry => entry.DailyEmotionRecordId == daily.Id)
                    || !state.PracticeGrowthRecords.Any(growth => growth.SessionId == daily.Id
                        && growth.Outcome == PracticeGrowthOutcome.Grown && growth.PlantInstanceId == photo.SourcePlantInstanceId
                        && growth.CycleId == daily.CycleId && growth.CommittedUtc == daily.CompletedUtc)) continue;
                var memo = new GardenPlantMemo { Id = "album-record:" + daily.Id, DailyEmotionRecordId = daily.Id,
                    PlantInstanceId = photo.SourcePlantInstanceId, CreatedUtc = daily.CompletedUtc, Mood = daily.Mood, Note = daily.Note };
                SetLegacyMission(state, daily, memo, true); SetLegacyMission(state, daily, memo, false);
                records.Add(memo);
            }
            return records.OrderByDescending(memo => memo.DailyEmotionRecordId, StringComparer.Ordinal)
                .ThenByDescending(memo => memo.CreatedUtc, StringComparer.Ordinal).ThenBy(memo => memo.Id, StringComparer.Ordinal).ToArray();
        }
        static void SetLegacyMission(GardenState state, DailyEmotionRecord daily, GardenPlantMemo memo, bool basic)
        {
            string sessionId = basic ? daily.BasicSessionId : daily.AdditionalSessionId;
            var session = state.Sessions.FirstOrDefault(item => item.SessionId == sessionId && item.InstanceId == daily.CycleId
                && item.Status == ParticipationState.RewardCommitted && FocusMissionPolicy.RoleFor(item)
                    == (basic ? FocusMissionCatalog.BasicRole : FocusMissionCatalog.AdditionalRole));
            if (session == null) return;
            if (basic) { memo.BasicSessionId = session.SessionId; memo.BasicMissionId = session.MissionId; memo.BasicMissionTitle = session.MissionTitleSnapshot ?? ""; }
            else { memo.AdditionalSessionId = session.SessionId; memo.AdditionalMissionId = session.MissionId; memo.AdditionalMissionTitle = session.MissionTitleSnapshot ?? ""; }
        }
    }
}
