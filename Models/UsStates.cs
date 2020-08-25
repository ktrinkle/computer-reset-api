using System;
using System.Collections.Generic;

namespace ComputerResetApi
{
    public partial class UsStates
    {
        public UsStates()
        {
            UsCities = new HashSet<UsCities>();
        }

        public int Id { get; set; }
        public string StateCode { get; set; }
        public string StateName { get; set; }

        public virtual ICollection<UsCities> UsCities { get; set; }
    }
}
