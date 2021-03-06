﻿using Start.Net.Constants;
using Start.Net.ResponseModels;
using Start.Net.Entities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using Start.Net.RequestModels.Charges;
using Start.Net.Interfaces;
using Start.Net.RequestModels.Customers;

namespace Start.Net.Tests
{
    [TestClass]
    public class ChargeServiceTests
    {
        private IStartChargeService _service;
        private CreateChargeRequest _createChargeRequest;
        private IStartCustomerService _customerService;
        private Card _workingCard; 

        public ChargeServiceTests()
        {
            _service = new StartChargeService("test_sec_k_8512e94e69d6a46c67ab2");
            _customerService = new StartCustomerService("test_sec_k_8512e94e69d6a46c67ab2");
            _workingCard = new Card()
            {
                Name = "Abdullah Ahmed",
                Cvc = 123,
                ExpireMonth = 12,
                ExpireYear = 2020,
                Number = "4242424242424242"
            };

            _createChargeRequest = new CreateChargeRequest()
            {
                Amount = 10000,
                Currency = Currency.AED,
                Email = "john.doe@gmail.com"
            };
        }

        [TestMethod]
        public void CreateCharge_UsingToken_FailsWithTokenAlreadyUsed()
        {
            _createChargeRequest.CardToken = "tok_99af1278b71929fb6c2268fce091";

            ApiResponse<Charge> response = _service.CreateCharge(_createChargeRequest);

            Assert.IsTrue(response.IsError);
            Assert.IsTrue(string.IsNullOrEmpty(response.Content.Id));
            Assert.IsTrue(response.Error.Type == ErrorType.Request);
            Assert.AreEqual("Request params are invalid.", response.Error.Message);
            Assert.IsTrue(response.Error.Extras == "{\r\n  \"card\": [\r\n    \"token has already been used\"\r\n  ]\r\n}");
        }

        [TestMethod]
        public void CreateCharge_UsingCardDetails_Success()
        {
            _createChargeRequest.CardDetails = _workingCard;

            ApiResponse<Charge> response = _service.CreateCharge(_createChargeRequest);

            Assert.IsTrue(!string.IsNullOrEmpty(response.Content.Id));
            Assert.IsTrue(response.Content.State == ChargeState.Authorized);
        }

        [TestMethod]
        public void CreateCharge_UsingCustomerId_Success()
        {
            var createCustomerRequest = new CreateCustomerRequest()
            {
                Name = "Customer Name",
                Description = "Description",
                Email = "customer@email.com",
                CardDetails = new Card()
                {
                    Name = "Abdullah Ahmed",
                    Cvc = 123,
                    ExpireMonth = 12,
                    ExpireYear = 2020,
                    Number = "4242424242424242"
                }
            };

            ApiResponse<Customer> customer = _customerService.CreateCustomer(createCustomerRequest);

            _createChargeRequest.CustomerId = customer.Content.Id;

            ApiResponse<Charge> response = _service.CreateCharge(_createChargeRequest);

            Assert.IsTrue(!string.IsNullOrEmpty(response.Content.Id));
            Assert.IsTrue(response.Content.State == ChargeState.Authorized);
        }

        [TestMethod]
        public void CreateCharge_AuthAndCapture_Success()
        {
            _createChargeRequest.CardDetails = _workingCard;
            _createChargeRequest.Capture = true;
            ApiResponse<Charge> response = _service.CreateCharge(_createChargeRequest);

            Assert.IsTrue(!string.IsNullOrEmpty(response.Content.Id));
            Assert.IsTrue(response.Content.State == ChargeState.Captured);
        }

        [TestMethod]
        public void CreateCharge_FailsWithRequestError()
        {
            _createChargeRequest.CardDetails = _workingCard;
            _createChargeRequest.CardDetails.ExpireYear = 2013;

            ApiResponse<Charge> response = _service.CreateCharge(_createChargeRequest);

            Assert.IsTrue(response.IsError);
            Assert.IsTrue(string.IsNullOrEmpty(response.Content.Id));
            Assert.IsTrue(response.Error.Type == ErrorType.Request);
            Assert.AreEqual("Request params are invalid.", response.Error.Message);
        }

        [TestMethod]
        public void CreateCharge_FailsWithBankingError()
        {
            _createChargeRequest.CardDetails = _workingCard;
            _createChargeRequest.CardDetails.Number = "4000000000000002";

            ApiResponse<Charge> response = _service.CreateCharge(_createChargeRequest);

            Assert.IsTrue(response.IsError);
            Assert.IsTrue(string.IsNullOrEmpty(response.Content.Id));
            Assert.IsTrue(response.Error.Type == ErrorType.Banking);
            Assert.AreEqual("Charge was declined.", response.Error.Message);
        }

        [TestMethod]
        public void CreateCharge_FailsWithAuthError()
        {
            _service = new StartChargeService("wrong_key");

            _createChargeRequest.CardDetails = _workingCard;

            ApiResponse<Charge> response = _service.CreateCharge(_createChargeRequest);

            Assert.IsTrue(response.IsError);
            Assert.IsTrue(string.IsNullOrEmpty(response.Content.Id));
            Assert.IsTrue(response.Error.Type == ErrorType.Authentication);
            Assert.AreEqual("Request can not be authenticated with provided API Key.", response.Error.Message);
        }

        [TestMethod]
        public void CreateCharge_FailsWith500Error()
        {
            _createChargeRequest.CardDetails = _workingCard;
            _createChargeRequest.CardDetails.Number = "4000000000000127";

            ApiResponse<Charge> response = _service.CreateCharge(_createChargeRequest);

            Assert.IsTrue(response.IsError);
            Assert.IsTrue(string.IsNullOrEmpty(response.Content.Id));
            Assert.IsTrue(response.Error.Type == ErrorType.Processing);
            Assert.AreEqual("Internal Server Error. We have been already notified about it.", response.Error.Message);
        }

        [TestMethod]
        public void GetCharge_Success()
        {
            _createChargeRequest.CardDetails = _workingCard;

            Charge createChargeResponse = _service.CreateCharge(_createChargeRequest).Content;

            GetChargeRequest chargeRequest = new GetChargeRequest();
            chargeRequest.ChargeId = createChargeResponse.Id;

            Charge getChargeResponse = _service.GetCharge(chargeRequest).Content;

            Assert.AreEqual(createChargeResponse.Id, getChargeResponse.Id);
        }

        [TestMethod]
        public void GetCharge_NotFound()
        {
            GetChargeRequest chargeRequest = new GetChargeRequest();
            chargeRequest.ChargeId = "not_found_id";

            ApiResponse<Charge> getChargeResponse = _service.GetCharge(chargeRequest);
            Assert.IsTrue(getChargeResponse.IsError);
            Assert.IsTrue(getChargeResponse.Error.Type == ErrorType.Request);
            Assert.AreEqual(string.Format("Couldn't find Charge with 'id'={0}.", chargeRequest.ChargeId), getChargeResponse.Error.Message);
            Assert.AreEqual(ErrorCode.NotFound, getChargeResponse.Error.Code);
        }

        [TestMethod]
        public void CaptureCharge_Success()
        {
            _createChargeRequest.CardDetails = _workingCard;

            Charge createChargeResponse = _service.CreateCharge(_createChargeRequest).Content;

            Assert.IsTrue(createChargeResponse.State == ChargeState.Authorized);

            CaptureChargeRequest captureRequest = new CaptureChargeRequest();
            captureRequest.ChargeId = createChargeResponse.Id;
            captureRequest.Amount = createChargeResponse.Amount;

            CaptureChargeResponse captureResponse = _service.CaptureCharge(captureRequest).Content;

            Assert.AreEqual(createChargeResponse.Id, captureResponse.ChargeId);
            Assert.AreEqual(createChargeResponse.Amount, captureResponse.ChargeAmount);
            Assert.IsTrue(captureResponse.State == ChargeState.Captured);

            GetChargeRequest getChargeRequest = new GetChargeRequest();
            getChargeRequest.ChargeId = captureRequest.ChargeId;
            Charge charge = _service.GetCharge(getChargeRequest).Content;

            Assert.AreEqual(charge.CapturedAmount, captureResponse.CapturedAmount);
            Assert.IsTrue(charge.State == ChargeState.Captured);
        }

        [TestMethod]
        public void CaptureCharge_WithMoreThanAuthorized_Fails()
        {
            _createChargeRequest.CardDetails = _workingCard;

            Charge createChargeResponse = _service.CreateCharge(_createChargeRequest).Content;
            Assert.IsTrue(createChargeResponse.State == ChargeState.Authorized);

            CaptureChargeRequest captureRequest = new CaptureChargeRequest();
            captureRequest.ChargeId = createChargeResponse.Id;
            captureRequest.Amount = createChargeResponse.Amount + 200;

            ApiResponse<CaptureChargeResponse> captureResponse = _service.CaptureCharge(captureRequest);
            Assert.IsTrue(captureResponse.IsError);
            Assert.IsTrue(captureResponse.Error.Type == ErrorType.Request);
            Assert.AreEqual("Request params are invalid.", captureResponse.Error.Message);
        }

        [TestMethod]
        public void CaptureCharge_ChargeAlreadyCaptured_Fails()
        {
            _createChargeRequest.CardDetails = _workingCard;
            _createChargeRequest.Capture = true;
            Charge createChargeResponse = _service.CreateCharge(_createChargeRequest).Content;

            CaptureChargeRequest captureRequest = new CaptureChargeRequest();
            captureRequest.ChargeId = createChargeResponse.Id;
            captureRequest.Amount = createChargeResponse.Amount;

            ApiResponse<CaptureChargeResponse> captureResponse = _service.CaptureCharge(captureRequest);
            Assert.IsTrue(captureResponse.IsError);
            Assert.IsTrue(captureResponse.Error.Type == ErrorType.Request);
            Assert.AreEqual("Only authorized charges can be captured.", captureResponse.Error.Message);
        }

        [TestMethod]
        public void ListCharges_Success()
        {
            _createChargeRequest.CardDetails = _workingCard;
            Charge createChargeResponse = _service.CreateCharge(_createChargeRequest).Content;
            _service.CreateCharge(_createChargeRequest);
            _service.CreateCharge(_createChargeRequest);

            ListChargesRequest request = new ListChargesRequest();
            PagedApiResponse<Charge> charges = _service.ListCharges(request);

            Assert.IsFalse(charges.IsError);
            Assert.IsTrue(charges.Content.Count >= 3);
        }

        [TestMethod]
        public void ListChages_PagedBefore_Success()
        {
            _createChargeRequest.CardDetails = _workingCard;
            Charge createChargeResponse = _service.CreateCharge(_createChargeRequest).Content;
            ListChargesRequest request = new ListChargesRequest();
            DateTime beforeFilter = DateTime.Now.ToUniversalTime();
            request.Before = beforeFilter;

            PagedApiResponse<Charge> charges = _service.ListCharges(request);

            Assert.IsFalse(charges.IsError);
            Assert.IsFalse(charges.Content.Any(c => c.Id == createChargeResponse.Id)); // charge should be missing because of pagination
        }

        [TestMethod]
        public void ListChages_PagedAfter_Success()
        {
            DateTime afterFilter = DateTime.Now.ToUniversalTime();
            _createChargeRequest.CardDetails = _workingCard;

            Charge createChargeResponse = _service.CreateCharge(_createChargeRequest).Content;
            ListChargesRequest request = new ListChargesRequest();
            
            request.After = afterFilter;

            PagedApiResponse<Charge> charges = _service.ListCharges(request);

            Assert.IsFalse(charges.IsError);
            Assert.IsTrue(charges.Content.Any(c => c.Id == createChargeResponse.Id)); // charge should be there because of pagination
        }
    }
}
