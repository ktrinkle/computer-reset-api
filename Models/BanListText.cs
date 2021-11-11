using System;
using System.Collections.Generic;

namespace ComputerResetApi
{
    public partial class BanListText
    {
        public int Id { get; set; }
        public string FirstNm { get; set; }
        public string LastNm { get; set; }
        public string CityNm { get; set; }
        public string StateCd { get; set; }
        public string CommentTxt { get; set; }
    }

    public partial class BanListForm
    {
        public int Id { get; set; }
        public string FirstNm { get; set; }
        public string LastNm { get; set; }
        public string CityNm { get; set; }
        public string StateCd { get; set; }
        public string CommentTxt { get; set; }
        public string FacebookId { get; set; }
    }
}
