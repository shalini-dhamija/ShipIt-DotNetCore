﻿using System;
using System.Collections.Generic;
using System.Linq;
 using Microsoft.AspNetCore.Mvc;
 using ShipIt.Exceptions;
using ShipIt.Models.ApiModels;
using ShipIt.Repositories;

namespace ShipIt.Controllers
{
    [Route("orders/outbound")]
    public class OutboundOrderController : ControllerBase
    {
        private static readonly log4net.ILog Log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType);

        private readonly IStockRepository _stockRepository;
        private readonly IProductRepository _productRepository;

        public OutboundOrderController(IStockRepository stockRepository, IProductRepository productRepository)
        {
            _stockRepository = stockRepository;
            _productRepository = productRepository;
        }

        [HttpPost("")]
        public Response Post([FromBody] OutboundOrderRequestModel request)
        {
            Log.Info(String.Format("Processing outbound order: {0}", request));

            //No duplicate gtins allowed
            var gtins = new List<String>();
            foreach (var orderLine in request.OrderLines)
            {
                if (gtins.Contains(orderLine.gtin))
                {
                    throw new ValidationException(String.Format("Outbound order request contains duplicate product gtin: {0}", orderLine.gtin));
                }
                gtins.Add(orderLine.gtin);
            }
            ////////////////
            //Order -> 1, {abc,100}{xyz,200}
            var productDataModels = _productRepository.GetProductsByGtin(gtins);//Going to DB (all the details for product abc and xyz)
            var products = productDataModels.ToDictionary(p => p.Gtin, p => new Product(p));//(DB is returing us, we are storing it as key value pair)
         
            var lineItems = new List<StockAlteration>();
            var itemsWithWeight = new List<StockWeight>();
            var productIds = new List<int>();
            var errors = new List<string>();

            foreach (var orderLine in request.OrderLines)
            {
                if (!products.ContainsKey(orderLine.gtin))//products abc & xyz exist in DB
                {
                    errors.Add(string.Format("Unknown product gtin: {0}", orderLine.gtin));
                }
                else
                {
                    var product = products[orderLine.gtin];
                    lineItems.Add(new StockAlteration(product.Id, orderLine.quantity));//preparing a list of items to to reduce from stock later
                    itemsWithWeight.Add(new StockWeight(product.Id,orderLine.gtin,orderLine.quantity,product.Weight/1000));
                    productIds.Add(product.Id);
                }
            }

            if (errors.Count > 0)
            {
                throw new NoSuchEntityException(string.Join("; ", errors));
            }
            //Getting stock for warehouseid and all product ids
            var stock = _stockRepository.GetStockByWarehouseAndProductIds(request.WarehouseId, productIds);

            var orderLines = request.OrderLines.ToList();
            // orderLines=[{abc,100}{xyz,200}]
            errors = new List<string>();

            //If we have sufficient quantity for product id to order
            for (int i = 0; i < lineItems.Count; i++)
            {
                var lineItem = lineItems[i];
                var orderLine = orderLines[i];

                if (!stock.ContainsKey(lineItem.ProductId))
                {
                    errors.Add(string.Format("Product: {0}, no stock held", orderLine.gtin));                    
                    continue;
                }

                var item = stock[lineItem.ProductId];
                if (lineItem.Quantity > item.held)//enough in stock to order
                {
                    errors.Add(
                        string.Format("Product: {0}, stock held: {1}, stock to remove: {2}", orderLine.gtin, item.held,
                            lineItem.Quantity));
                }
            }

            if (errors.Count > 0)
            {
                throw new InsufficientStockException(string.Join("; ", errors));
            }

            _stockRepository.RemoveStock(request.WarehouseId, lineItems);

            var truckId = 1;
            var truck = new Truck
            {
                Id = truckId++
            };

            var maxWeightPerTruck = 200;
            
            var response = new OutboundOrdersResponse();

            foreach(var item in itemsWithWeight)
            {
                for(var i=1; i<=item.Quantity; i++)
                {
                    if((truck.Weight+item.WeightInKg)>maxWeightPerTruck)
                    {
                        response.Trucks.Add(truck);                     
                        truck = new Truck
                        {
                            Id = truckId++
                        };                    
                    }
                    truck.Weight += item.WeightInKg;
                    var orderLine = new OrderLine()
                    {
                        gtin = item.Gtin,
                        quantity = 1
                    };
                    truck.Products.Add(orderLine);                   
                }
            }
            response.Trucks.Add(truck);
            foreach(var t in response.Trucks)
            {
                t.Products = t.Products
                                .GroupBy(p=>p.gtin)
                                .Select(group=>new OrderLine{gtin=group.Key,quantity=group.Count()})
                                .ToList();
            }
            response.Success = true;
            return response;
        }
    }
}