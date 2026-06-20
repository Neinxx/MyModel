using UnityEngine;

namespace Mainboard.Runtime
{
    internal sealed class MainboardLogger
    {
        private readonly Object _context;

        public MainboardLogger(Object context)
        {
            _context = context;
        }

        public void Info(string message)
        {
            Debug.Log(Format("#7C8CFF", message), _context);
        }

        public void Success(string message)
        {
            Debug.Log(Format("#3FB950", message), _context);
        }

        public void Warning(string message)
        {
            Debug.LogWarning(Format("#FFB443", message), _context);
        }

        public void Error(string message)
        {
            Debug.LogError(Format("#FF5252", message), _context);
        }

        private static string Format(string color, string message)
        {
            return $"<color={color}><b>[Mainboard]</b></color> {message}";
        }
    }
}
