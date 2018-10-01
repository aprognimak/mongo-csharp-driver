using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EntityKeyType = System.Guid;
using EnumKeyType = System.Int64;

namespace Ownable.Entities
{
    public class Product 
    {
        public Guid Id { get; set; }

        public string Brand { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime? LastAvailableTime { get; set; }
        public int UnknownCount { get; set; }

        /// <summary>
        /// Original price imported from Zinc
        /// </summary>
        [DataType(DataType.Currency)]
        [DisplayName("Orig.ext.price")]
        public decimal? OriginalCatalogPrice { get; set; }

        /// <summary>
        /// Current Price from Zinc
        /// </summary>
        [DataType(DataType.Currency)]
        [DisplayName("Ext.price")] // same as original price 
        [Description("External catalog price")]
        public decimal? CurrentCatalogPrice { get; set; }

        /// <summary>
        /// Price with applied ownable rules (gross margin)
        /// </summary>
        [DataType(DataType.Currency)]
        [DisplayName("Price")] // same as original price 
        [Description("Current price the users see")]
        public decimal? CurrentPrice { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime LastPriceUpdateTime { get; set; }


        [DataType("Rating")]
        public double? Rating { get; set; }

        public IEnumerable<string> Images { get; set; }

        public IEnumerable<string> Features { get; set; }

        [DisplayName("Promoted")]
        public bool IsPromoted { get; set; }

        [Required]
        [ForeignKey("ProductCategory")]
        [Browsable(false)]
        [DisplayName("Category")]
        public EntityKeyType ProductCategoryId { get; set; }
        public virtual ProductCategory ProductCategory { get; set; }

        public Dictionary<string, object> ExtraElements { get; set; }
    }
}