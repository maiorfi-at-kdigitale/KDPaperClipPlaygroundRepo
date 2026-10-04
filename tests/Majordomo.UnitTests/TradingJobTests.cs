using Majordomo.SharedKernel;
using Majordomo.Strategy.Contracts;
using Majordomo.Strategy.Domain;
using Shouldly;
using Xunit;

namespace Majordomo.UnitTests;

public sealed class TradingJobTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    private static TradingJob NewJob() => TradingJob.Create("Job di test", null, "{}", Now);

    [Fact]
    public void Nuovo_job_parte_in_draft_con_versione_parametri_1()
    {
        var job = NewJob();
        job.Status.ShouldBe(JobStatus.Draft);
        job.ParameterSetVersion.ShouldBe(1);
    }

    [Fact]
    public void Promozione_a_paper_senza_evidenza_e_rifiutata()
    {
        var job = NewJob();
        job.Apply(TransitionAction.Promote, null, hasEvidence: false, Now);
        job.Status.ShouldBe(JobStatus.Backtesting);

        var ex = Should.Throw<DomainException>(() => job.Apply(TransitionAction.Promote, null, hasEvidence: false, Now));
        ex.Code.ShouldBe("evidence_required");
    }

    [Fact]
    public void Pausa_e_ripresa_ritornano_allo_stato_precedente()
    {
        var job = NewJob();
        job.Apply(TransitionAction.Promote, null, false, Now);
        job.Apply(TransitionAction.Promote, null, true, Now);
        job.Apply(TransitionAction.Pause, "manutenzione", false, Now);
        job.Status.ShouldBe(JobStatus.Paused);
        job.Apply(TransitionAction.Resume, null, false, Now);
        job.Status.ShouldBe(JobStatus.PaperTrading);
    }

    [Fact]
    public void Parametri_non_modificabili_mentre_il_job_opera()
    {
        var job = NewJob();
        job.Apply(TransitionAction.Promote, null, false, Now);
        Should.Throw<DomainException>(() => job.ReplaceParameters("{}", Now));
    }

    [Fact]
    public void Halt_genera_evento_e_ripresa_richiede_risk_admin()
    {
        var job = NewJob();
        job.Apply(TransitionAction.Promote, null, false, Now);
        job.Apply(TransitionAction.Promote, null, true, Now);
        job.ClearDomainEvents();

        job.Halt("kill switch", Now).ShouldBeTrue();
        job.Status.ShouldBe(JobStatus.Halted);
        job.DomainEvents.OfType<JobStatusChanged>().Single().To.ShouldBe(JobStatus.Halted);
        job.RequiresRiskAdmin(TransitionAction.Resume).ShouldBeTrue();
    }

    [Fact]
    public void Archiviazione_non_ammessa_da_live()
    {
        var job = NewJob();
        job.Apply(TransitionAction.Promote, null, false, Now);
        job.Apply(TransitionAction.Promote, null, true, Now);
        job.Apply(TransitionAction.Promote, null, true, Now);
        job.Status.ShouldBe(JobStatus.Live);
        Should.Throw<DomainException>(() => job.Apply(TransitionAction.Archive, null, false, Now));
    }
}
