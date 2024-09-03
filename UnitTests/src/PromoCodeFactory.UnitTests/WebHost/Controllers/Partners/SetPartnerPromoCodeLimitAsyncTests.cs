using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoFixture;
using AutoFixture.AutoMoq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PromoCodeFactory.Core.Abstractions.Repositories;
using PromoCodeFactory.Core.Domain.PromoCodeManagement;
using PromoCodeFactory.WebHost.Controllers;
using PromoCodeFactory.WebHost.Models;
using Xunit;

namespace PromoCodeFactory.UnitTests.WebHost.Controllers.Partners
{
    public class SetPartnerPromoCodeLimitAsyncTests
    {
        private readonly Mock<IRepository<Partner>> _partnersRepositoryMock;
        private readonly PartnersController _partnersController;
        private readonly IFixture _fixture;

        public SetPartnerPromoCodeLimitAsyncTests()
        {
            _fixture = new Fixture().Customize(new AutoMoqCustomization());
            _partnersRepositoryMock = _fixture.Freeze<Mock<IRepository<Partner>>>();
            _partnersController = _fixture.Build<PartnersController>().OmitAutoProperties().Create();
        }

        //Если партнер не найден, то также нужно выдать ошибку 404;
        [Fact]
        public async void SetPartnerPromoCodeLimitAsync_PartnerIsNotFound_ReturnsNotFound()
        {
            // Arrange
            var partnerId = Guid.Parse("def47943-7aaf-44a1-ae21-05aa4948b165");
            Partner partner = null;

            _partnersRepositoryMock.Setup(repo => repo.GetByIdAsync(partnerId))
                .ReturnsAsync(partner);

            // Act
            var result = await _partnersController.SetPartnerPromoCodeLimitAsync(partnerId, null);

            // Assert
            result.Should().BeAssignableTo<NotFoundResult>();
        }

        // Если партнер заблокирован, то есть поле IsActive=false в классе Partner, то также нужно выдать ошибку 400;
        [Fact]
        public async void SetPartnerPromoCodeLimitAsync_WhenIsActiveEqualFalse_ReturnsNotFound()
        {
            // Arrange
            var partnerId = Guid.Parse("def47943-7aaf-44a1-ae21-05aa4948b165");

            var partner = _fixture.Build<Partner>()
                .OmitAutoProperties()
                .With(p => p.Id, partnerId)
                .With(p => p.IsActive, false)
                .Create();

            _partnersRepositoryMock.Setup(repo => repo.GetByIdAsync(partnerId))
                .ReturnsAsync(partner);

            // Act
            var result = await _partnersController.SetPartnerPromoCodeLimitAsync(partnerId, null);

            // Assert
            result.Should().BeAssignableTo<BadRequestObjectResult>();
        }

        // Если партнеру выставляется лимит, то мы должны обнулить количество промокодов, которые партнер выдал NumberIssuedPromoCodes, если лимит закончился, то количество не обнуляется;
        [Fact]
        public async void SetPartnerPromoCodeLimitAsync_IfSetLimit_SetNumberIssuedPromoCodesToZero()
        {
            //Arrange
            var partnerId = Guid.Parse("def47943-7aaf-44a1-ae21-05aa4948b165");
            var partnerPromoCodeLimit = _fixture.Build<PartnerPromoCodeLimit>()
                .OmitAutoProperties()
                .With(x => x.Id, Guid.NewGuid())
                .With(x => x.Limit, _fixture.Create<int>())
                .Create();

            var partner = _fixture.Build<Partner>()
                .OmitAutoProperties()
                .With(x => x.Id, partnerId)
                .With(x => x.IsActive, true)
                .With(x => x.PartnerLimits, new List<PartnerPromoCodeLimit>() { partnerPromoCodeLimit })
                .Create();

            _partnersRepositoryMock.Setup(repo => repo.GetByIdAsync(partnerId))
                .ReturnsAsync(partner);

            var request = _fixture.Build<SetPartnerPromoCodeLimitRequest>()
                .OmitAutoProperties()
                .With(x => x.Limit, 0)
                .Create();

            //Act
            var result = await _partnersController.SetPartnerPromoCodeLimitAsync(partnerId, request);

            //Assert
            result.Should().NotBeNull();

            result
                .Should().BeOfType<BadRequestObjectResult>()
                .Subject.Value.Should().Be("Лимит должен быть больше 0");

            partner.NumberIssuedPromoCodes.Should().Be(0);
        }

        // При установке лимита нужно отключить предыдущий лимит;
        [Fact]
        public async Task SetPartnerPromoCodeLimitAsync_IfSetLimit_CancelPreviousLimit()
        {
            //Arrange
            var partnerId = Guid.Parse("def47943-7aaf-44a1-ae21-05aa4948b165");

            var partnerLimit = _fixture.Build<PartnerPromoCodeLimit>()
                .OmitAutoProperties()
                .Create();

            var partner = _fixture.Build<Partner>()
                .OmitAutoProperties()
                .With(x => x.Id, partnerId)
                .With(x => x.IsActive, true)
                .With(x => x.PartnerLimits, new List<PartnerPromoCodeLimit>() { partnerLimit })
                .Create();

            _partnersRepositoryMock.Setup(repo => repo.GetByIdAsync(partnerId))
                .ReturnsAsync(partner);

            var request = _fixture.Build<SetPartnerPromoCodeLimitRequest>()
                .OmitAutoProperties()
                .Create();

            //Act
            var result = await _partnersController.SetPartnerPromoCodeLimitAsync(partnerId, request);

            //Assert
            result.Should().NotBeNull();
            partner.PartnerLimits.FirstOrDefault().CancelDate.Should().NotBeNull();
        }

        // Лимит должен быть больше 0;

        // Нужно убедиться, что сохранили новый лимит в базу данных (это нужно проверить Unit-тестом);
        // Если в текущей реализации найдутся ошибки, то их нужно исправить и желательно написать тест, чтобы они больше не повторялись.

    }
}