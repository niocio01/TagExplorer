using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TagExplorer.Models;

namespace TagExplorer
{
    public interface ITableObject
    {
        /// <summary>
        /// Id's are used to identify the object in the database
        /// </summary>
        public int? Id { get; protected set; }
    }
}
