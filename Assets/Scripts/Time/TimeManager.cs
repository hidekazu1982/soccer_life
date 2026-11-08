using System;
using SoccerClub.Data;

namespace SoccerClub.Time
{
    public interface ITimeService
    {
        DateTime CurrentDate { get; }
        int DayOfMonth { get; }
        bool IsTransferWindow { get; }
        void AdvanceDay();
        void SkipTransferWindow();
    }

    /// <summary>1ターン = 1日。時間経過で自動進行する。</summary>
    public sealed class TimeManager : ITimeService
    {
        public DateTime CurrentDate { get; private set; }
        public event Action<DateTime> OnDayAdvanced;
        public event Action<DateTime> OnMonthStarted;
        public event Action<DateTime> OnTransferWindowOpened;
        public event Action<DateTime> OnTransferWindowClosed;

        public TimeManager(DateTime seasonStart)
        {
            CurrentDate = seasonStart.Date;
        }

        public int DayOfMonth => CurrentDate.Day;

        /// <summary>簡易カレンダー: 7月と1月を移籍期間とする。</summary>
        public bool IsTransferWindow => CurrentDate.Month == 7 || CurrentDate.Month == 1;

        public void AdvanceDay()
        {
            bool wasWindow = IsTransferWindow;
            var previous = CurrentDate;
            CurrentDate = CurrentDate.AddDays(1);
            if (CurrentDate.Day == 1) OnMonthStarted?.Invoke(CurrentDate);
            OnDayAdvanced?.Invoke(CurrentDate);

            if (!wasWindow && IsTransferWindow) OnTransferWindowOpened?.Invoke(CurrentDate);
            if (wasWindow && !IsTransferWindow) OnTransferWindowClosed?.Invoke(CurrentDate);
            _ = previous;
        }

        public void SkipTransferWindow()
        {
            if (!IsTransferWindow) return;
            while (IsTransferWindow) AdvanceDay();
        }
    }
}
