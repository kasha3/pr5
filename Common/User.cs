using System;

namespace Common
{
    public class User
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Password { get; set; }
        public string Login { get; set; }
        public bool IsBlocked { get; set; } = false;
    }
}
