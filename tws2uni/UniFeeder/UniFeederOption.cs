using System.Collections.Generic;

namespace Tws2UniFeeder
{
    public class UniFeederOption
    {
        public string Ip { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 2222;
        public IList<UniFeederAuthorizationOption> Authorization { get; set; } = new List<UniFeederAuthorizationOption>();
    }

    public class UniFeederAuthorizationOption
    {
        public string Login { get; set; }
        public string Password { get; set; }

        public bool IsFilled
        {
            get
            {
                return !(string.IsNullOrWhiteSpace(this.Login) || string.IsNullOrWhiteSpace(this.Password)); 
            }
        }

        public bool Equals(UniFeederAuthorizationOption obj)
        {
            if (obj is null) return false;

            return (
                obj.Login == this.Login &&
                obj.Password == this.Password
            );
        }
    }
}
