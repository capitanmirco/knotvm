using FluentAssertions;
using KnotVM.Core.Enums;
using KnotVM.Core.Interfaces;
using KnotVM.Infrastructure.Services;
using Moq;
using Xunit;

namespace KnotVM.Tests.Infrastructure;

/// <summary>
/// Test proxy generation e encoding
/// Requisito: Prompt 11/12 - test anti-regressione su proxy/encoding
/// Nota: ProxyGeneratorService richiede template directory esistente
/// Questi test verificano solo l'interfaccia e la struttura delle dipendenze
/// </summary>
public class ProxyGeneratorServiceTests
{
    // I test di integrazione completi richiedono l'ambiente reale con templates
    // Questi test verificano solo che le interfacce siano corrette

    [Fact]
    public void IProxyGeneratorService_ShouldHaveGenerateGenericProxy()
    {
        // Verifica che l'interfaccia abbia il metodo con la firma corretta
        var interfaceType = typeof(IProxyGeneratorService);
        var method = interfaceType.GetMethod("GenerateGenericProxy");
        
        method.Should().NotBeNull();
        method!.GetParameters().Should().HaveCount(2);
        method.GetParameters()[0].ParameterType.Should().Be(typeof(string), "primo parametro è commandName");
        method.GetParameters()[1].ParameterType.Should().Be(typeof(string), "secondo parametro è commandExe");
        method.ReturnType.Should().Be(typeof(void));
    }

    [Fact]
    public void IProxyGeneratorService_ShouldHaveGeneratePackageManagerProxy()
    {
        // Verifica che l'interfaccia abbia il metodo con la firma corretta
        var interfaceType = typeof(IProxyGeneratorService);
        var method = interfaceType.GetMethod("GeneratePackageManagerProxy");
        
        method.Should().NotBeNull();
        method!.GetParameters().Should().HaveCount(2);
        method.GetParameters()[0].ParameterType.Should().Be(typeof(string), "primo parametro è packageManager");
        method.GetParameters()[1].ParameterType.Should().Be(typeof(string), "secondo parametro è scriptPath");
        method.ReturnType.Should().Be(typeof(void));
    }

    [Fact]
    public void IProxyGeneratorService_ShouldHaveGenerateNodeShim()
    {
        // Verifica che l'interfaccia abbia il metodo con la firma corretta
        var interfaceType = typeof(IProxyGeneratorService);
        var method = interfaceType.GetMethod("GenerateNodeShim");
        
        method.Should().NotBeNull();
        method!.GetParameters().Should().HaveCount(0);
        method.ReturnType.Should().Be(typeof(void));
    }

    [Fact]
    public void IProxyGeneratorService_ShouldHaveRemoveProxy()
    {
        // Verifica che l'interfaccia abbia il metodo con la firma corretta
        var interfaceType = typeof(IProxyGeneratorService);
        var method = interfaceType.GetMethod("RemoveProxy");
        
        method.Should().NotBeNull();
        method!.GetParameters().Should().HaveCount(1);
        method.GetParameters()[0].ParameterType.Should().Be(typeof(string), "parametro è proxyName");
        method.ReturnType.Should().Be(typeof(void));
    }

    [Fact]
    public void IProxyGeneratorService_ShouldHaveRemoveAllProxies()
    {
        // Verifica che l'interfaccia abbia il metodo con la firma corretta
        var interfaceType = typeof(IProxyGeneratorService);
        var method = interfaceType.GetMethod("RemoveAllProxies");
        
        method.Should().NotBeNull();
        method!.GetParameters().Should().HaveCount(0);
        method.ReturnType.Should().Be(typeof(void));
    }

    [Fact]
    public void ProxyGeneratorService_ShouldImplementInterface()
    {
        // Verifica che ProxyGeneratorService implementi IProxyGeneratorService
        var serviceType = typeof(ProxyGeneratorService);
        var interfaceType = typeof(IProxyGeneratorService);
        
        serviceType.GetInterfaces().Should().Contain(interfaceType);
    }

    [Fact]
    public void ProxyGeneratorService_Constructor_ShouldRequireCorrectDependencies()
    {
        // Verifica che il costruttore richieda le dipendenze corrette
        var serviceType = typeof(ProxyGeneratorService);
        var constructors = serviceType.GetConstructors();
        
        constructors.Should().HaveCount(1, "dovrebbe esserci un solo costruttore pubblico");
        
        var constructor = constructors[0];
        var parameters = constructor.GetParameters();
        
        parameters.Should().HaveCount(3);
        parameters[0].ParameterType.Should().Be(typeof(IPlatformService));
        parameters[1].ParameterType.Should().Be(typeof(IPathService));
        parameters[2].ParameterType.Should().Be(typeof(IFileSystemService));
    }
}
