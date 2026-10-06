namespace Abril_Backend.Infrastructure.Interfaces
{
    public interface IStorageContainerResolver
    {
        string GetLessonsContainerName();
        string GetIvtContainerName();
        string GetConstructionSiteLogbookContainerName();
        string GetResidentIncidentContainerName();
        string GetProjectSubContractorContainerName();
        string GetProjectFotosContainerName();
        string GetProjectCroquisContainerName();
        string GetProjectLogoContainerName();
        string GetVecinoRequisitosContainerName();
        string GetVecinoEntregablesContainerName();
        string GetVecinoPropiedadImagenesContainerName();
        string GetInvoicesContainerName();
        string GetActasReunionContainerName();
        string GetTareosContainerName();
        string GetEppImagenesContainerName();
        string GetEppFichasTecnicasContainerName();
        string GetSenaleticaImagenesContainerName();
        string GetResiduosContainerName();
        string GetCursoImagenesContainerName();
        string GetAtsContainerName();
    }
}