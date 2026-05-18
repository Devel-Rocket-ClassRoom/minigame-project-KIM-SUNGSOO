using System;
using System.Collections.Generic;

namespace KRTD.Core
{
    /// <summary>
    /// 타입 기반 정적 이벤트 버스.
    /// 매니저 간 강결합을 피하기 위한 채널. (예: EnemyDied, WaveCleared 등)
    /// 사용: EventBus.Subscribe&lt;EnemyDiedEvent&gt;(OnEnemyDied);
    ///        EventBus.Raise(new EnemyDiedEvent(...));
    /// </summary>
    public static class EventBus
    {
        private static readonly Dictionary<Type, Delegate> handlers = new();

        public static void Subscribe<T>(Action<T> handler)
        {
            var type = typeof(T);
            if (handlers.TryGetValue(type, out var existing))
                handlers[type] = Delegate.Combine(existing, handler);
            else
                handlers[type] = handler;
        }

        public static void Unsubscribe<T>(Action<T> handler)
        {
            var type = typeof(T);
            if (!handlers.TryGetValue(type, out var existing)) return;
            var remaining = Delegate.Remove(existing, handler);
            if (remaining == null) handlers.Remove(type);
            else handlers[type] = remaining;
        }

        public static void Raise<T>(T evt)
        {
            if (handlers.TryGetValue(typeof(T), out var d))
                ((Action<T>)d)?.Invoke(evt);
        }

        public static void Clear() => handlers.Clear();
    }
}
