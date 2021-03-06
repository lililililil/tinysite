﻿using System.Collections.Generic;
using TinySite.Extensions;

namespace TinySite.Models
{
    public class Paginator
    {
        public IEnumerable<dynamic> Documents { get; set; }

        public Pagination Pagination { get; set; }

        public dynamic GetAsDynamic()
        {
            var data = new CaseInsensitiveExpando();

            data["Documents"] = this.Documents;
            data["Pagination"] = this.Pagination;

            return data;
        }
    }
}
