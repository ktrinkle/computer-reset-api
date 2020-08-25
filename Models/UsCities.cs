using System;
using System.Collections.Generic;

namespace ComputerResetApi
{
    public partial class UsCities
    {
        public int Id { get; set; }
        public int IdState { get; set; }
        public string City { get; set; }
        public string County { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        public virtual UsStates IdStateNavigation { get; set; }
    }
}
