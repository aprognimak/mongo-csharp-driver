using System;
using System.Collections.Generic;
using System.ComponentModel;
using Ownable.Entities;

namespace Ownable.DataObjects.ProductCategory
{
    public class ProductCategoryListItem 
    {
        public Guid Id { get; set; }

        public virtual string Name { get; set; }
        public virtual string Description { get; set; }
        public EntityEnumDto Parent { get; set; }
        public virtual bool IsPromoted { get; set; }

        //[DisplayName("# products")]
        //[UIMetadata(SortPropertyPath = "products.Count")]
        //public int ProductsCount { get; set; }

        public IEnumerable<EntityEnumDto> Products { get; set; }
        public int ProductsCount { get; set; }
    }
}
