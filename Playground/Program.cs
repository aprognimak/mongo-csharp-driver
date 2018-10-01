using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Ownable.DataObjects;
using Ownable.DataObjects.ProductCategory;
using Ownable.Entities;

namespace Playground
{
    public class MongoDbTID<TOuter, TInner>
    {
        public MongoDbTID()
        {

        }

        public MongoDbTID(TOuter o, TInner i)
        {
            this.Outer = o;
            this.Inner = i;
        }

        public TOuter Outer;
        public TInner Inner;
    }

    internal struct MongoTIDStruct<TOuter, TInner>
    {
        public TOuter Outer;
        public TInner Inner;
    }

    class Program
    {
        static Product ProcessProduct(MongoDbTID<Product, ProductCategory> pcat)
        {
            pcat.Outer.ProductCategory = pcat.Inner;
            return pcat.Outer;
        }

        static Product ProcessProduct(MongoTIDStruct<Product, ProductCategory> pcat)
        {
            pcat.Outer.ProductCategory = pcat.Inner;
            return pcat.Outer;
        }

        private static MongoTIDStruct<Product, ProductCategory> CallConstructTransparentIdentifier(Product p, ProductCategory cat)
        {
            return new MongoTIDStruct<Product, ProductCategory>() { Outer = p, Inner = cat };
        }

        static void Main(string[] args)
        {
            var mongoClient = new MongoDB.Driver.MongoClient("mongodb://localhost:27017");
            var mongoDb = mongoClient.GetDatabase("ownable_prod");

            BsonClassMap.RegisterClassMap<Product>().MapExtraElementsProperty("ExtraElements");
            BsonClassMap.RegisterClassMap<ProductCategory>().MapExtraElementsProperty("ExtraElements");
            BsonClassMap.RegisterClassMap<EntityEnumDto>(map =>
            {
                map.MapProperty(dto => dto.Id).SetElementName("Id");
                map.MapProperty(dto => dto.Code).SetElementName("Code");
                map.MapProperty(dto => dto.Name).SetElementName("Name");
            });

            List<Guid> productIds = new List<Guid>()
            {
                Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),
            };

            var queryProducts = mongoDb.GetCollection<Product>("Product").AsQueryable();

            var groupedBy = queryProducts.GroupBy(e => e.ProductCategoryId)
                .Select(e => new
                {
                    CategoryId = e.Key,
                    ProductCount = e.Count(),
                    TotalPrice = e.Sum(r => r.CurrentPrice),
                    Avg = e.Average(d => d.CurrentPrice)
                });

            var queryProductCategories = mongoDb.GetCollection<ProductCategory>("ProductCategory").AsQueryable().AsQueryable();
            var queryNonPromotedCategories = queryProductCategories.Where(e => !e.IsPromoted);

            Expression<Func<MongoDbTID<Product, ProductCategory>, Product>> selectorObject = tid => ProcessProduct(tid);
            Expression<Func<MongoTIDStruct<Product, ProductCategory>, Product>> selectorStruct = tid => ProcessProduct(tid);

            var queryJoin = queryProducts.Join(queryProductCategories, product => product.ProductCategoryId,
                category => category.Id, (product, category) => new { product, category });

            var queryJoinSelectProduct = queryProducts.Join(queryProductCategories, product => product.ProductCategoryId,
                category => category.Id, (product, category) => new { product, category }).Select(e => e.product);

            var queryJoinPromoted = queryProducts.Join(queryNonPromotedCategories, product => product.ProductCategoryId,
                category => category.Id, (product, category) => new MongoDbTID<Product, ProductCategory>()
                {
                    Outer = product,
                    Inner = category
                }).Select(selectorObject);

            var queryJoinWithStruct = queryProducts.Join(queryNonPromotedCategories, product => product.ProductCategoryId,
                category => category.Id, (product, category) => new MongoTIDStruct<Product, ProductCategory>()
                {
                    Outer = product,
                    Inner = category
                }).Select(selectorStruct);

            var queryJoinWithCall = queryProducts.Join(queryNonPromotedCategories, p => p.ProductCategoryId,
                p => p.Id, (product, category) => CallConstructTransparentIdentifier(product, category)).Select(e => e.Inner);

            var queryJoinWithConstruct = queryProducts.Join(queryNonPromotedCategories, product => product.ProductCategoryId,
                category => category.Id, (product, category) => new MongoDbTID<Product, ProductCategory>() { Outer = product, Inner = category }).Select(e => e.Outer.Name);

            var queryJoinWithAnon = queryProducts.Join(queryNonPromotedCategories, product => product.ProductCategoryId,
                category => category.Id, (product, category) => new { product, category }).Select(e => e.product.Name);

            Expression<Func<ProductCategory, ProductCategory, ProductCategoryListItem>> productCategoryProjector = (cat, parent) =>
                new ProductCategoryListItem()
                {
                    Id = cat.Id,
                    Name = cat.Name,
                    Parent = (parent ?? null) == null
                        ? null
                        : new EntityEnumDto()
                        {
                            Id = parent.Id,
                            Name = parent.Name
                        }
                };

            var queryCategoriesWithParent =
                from cat in queryProductCategories
                join catJoined in queryProductCategories on cat.ParentId equals catJoined.Id into parentCats
                from parent in parentCats.DefaultIfEmpty()
                select new ProductCategoryListItem()
                {
                    Id = cat.Id,
                    Name = cat.Name,
                    Parent = (parent ?? null) == null
                        ? null
                        : new EntityEnumDto()
                        {
                            Id = parent.Id,
                            Name = parent.Name
                        }
                };

            var queryCategoriesWithParentWithProducts =
                from cat in queryProductCategories
                join catJoined in queryProductCategories on cat.ParentId equals catJoined.Id into parentCats
                from parent in parentCats.DefaultIfEmpty()
                join product in queryProducts.Where(e=>e.IsPromoted) on cat.Id equals product.ProductCategoryId into products
                select new ProductCategoryListItem()
                {
                    Id = cat.Id,
                    Name = cat.Name,
                    Parent = (parent ?? null) == null
                        ? null
                        : new EntityEnumDto()
                        {
                            Id = parent.Id,
                            Name = parent.Name
                        },
                    Products = products.Select(e=>new EntityEnumDto()
                    {
                        Id = e.Id,
                        Name = e.Name
                    }),
                    ProductsCount = products.Count(e => e.IsPromoted)
                };

            var queryCategoriesWithParentAlt =
                from cat in queryProductCategories
                from parent in queryProductCategories.Where(p => p.Id == cat.ParentId).DefaultIfEmpty()
                select new ProductCategoryListItem()
                {
                    Id = cat.Id,
                    Name = cat.Name,
                    Parent = (parent ?? null) == null
                        ? null
                        : new EntityEnumDto()
                        {
                            Id = parent.Id,
                            Name = parent.Name
                        }
                };

            try
            {
                var prods = queryProducts.Where(e => e.LastPriceUpdateTime.AddMinutes(10) < DateTime.Now && e.Name.StartsWith("T")).ToList();
                var categoriesWithParent = queryCategoriesWithParent.ToList();
                var categoriesWithParentWithProducts = queryCategoriesWithParentWithProducts.ToList();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            try
            {
                //var grouped = groupedBy.ToList();

                //var joinedWithStruct = queryJoinWithStruct.ToList();

                //var joinedWithAnon = queryJoinWithAnon.ToList();
                var joinedWithConstruct = queryJoinWithConstruct.ToList();
                var joinedWithCall = queryJoinWithCall.ToList();

                //var joinedWithCallWithSelect = queryJoinWithCall.Select(selectorStruct).ToList();

                var productsOnly = queryJoinSelectProduct.ToList();

                Stopwatch sw = Stopwatch.StartNew();
                //var allProducts = queryProducts.ToList();
                sw.Stop();
                Console.WriteLine("All products: " + sw.Elapsed);

                sw.Restart();
                //var allProducts2 = queryProducts.ToList();
                Console.WriteLine("All products 2: " + sw.Elapsed);

                sw.Restart();
                var nonPromoted = queryJoinPromoted.ToList();
                Console.WriteLine("Non-promoted products: " + sw.Elapsed);

                sw.Restart();
                var nonPromoted2 = queryJoinPromoted.ToList();
                Console.WriteLine("Non-promoted products 2: " + sw.Elapsed);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}
