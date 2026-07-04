using System.Windows.Documents;
using CanXe.Application.Models;

namespace CanXe.Desktop.Services;

public interface IWeighTicketDocumentFactory
{
    FixedDocument CreateDocument(WeighTicketPrintModel model);
}
