using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using EntityKeyType = System.Guid;
using EnumKeyType = System.Int64;

namespace Ownable.Entities
{
    public class ProductCategory 
    {
        public Guid Id { get; set; }

        [Required]
        public string Name { get; set; }

        [DataType(DataType.MultilineText)]
        public string Description { get; set; }

        [DisplayName("Promoted")]
        public bool IsPromoted { get; set; }

        public string ImageUrl { get; set; }
        
        [ForeignKey("Parent")]
        public EntityKeyType? ParentId { get; set; }
        public virtual ProductCategory Parent { get; set; }

        [InverseProperty("Parent")]
        public virtual ICollection<ProductCategory> ChildCategories { get; set; }
        public virtual ICollection<Product> Products { get; set; }
    }
}
