using System;
using System.Diagnostics;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Ownable.Entities;

namespace Playground
{
    class Program
    {
        static void Main(string[] args)
        {
            var mongoClient = new MongoDB.Driver.MongoClient("mongodb://localhost:27017");
            var mongoDb = mongoClient.GetDatabase("ownable_prod");

            BsonClassMap.RegisterClassMap<Product>().SetIgnoreExtraElements(true);
            BsonClassMap.RegisterClassMap<ProductCategory>().SetIgnoreExtraElements(true);

            var queryProducts = mongoDb.GetCollection<Product>("Product").AsQueryable();
            var queryProductCategories = mongoDb.GetCollection<ProductCategory>("ProductCategory").AsQueryable();
            var queryPromotedCategories = queryProductCategories.Where(e => !e.IsPromoted);

            var queryJoin = queryProducts.Join(queryProductCategories, product => product.ProductCategoryId,
                category => category.Id, (product, category) => new {product, category});

            var queryJoinPromoted = queryProducts.Join(queryPromotedCategories, product => product.ProductCategoryId,
                category => category.Id, (product, category) => category);

            try
            {
                //var products = queryJoin.ToList();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            try
            {
                
                Stopwatch sw = Stopwatch.StartNew();
                var allProducts = queryProducts.ToList();
                sw.Stop();
                Console.WriteLine("All products: " + sw.Elapsed);

                sw.Restart();
                var allProducts2 = queryProducts.ToList();
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
