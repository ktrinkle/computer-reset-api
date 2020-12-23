using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;


namespace ComputerResetApi.Models
{
    public partial class cr9525signupContext : DbContext
    {
        private readonly ILogger _logger;
        public static readonly ILoggerFactory CrLoggerFactory = 
            LoggerFactory.Create(builder => { builder.AddConsole(); });

        public cr9525signupContext(ILogger<cr9525signupContext> logger)
        {
            _logger = logger;
        }

        public cr9525signupContext(DbContextOptions<cr9525signupContext> options, ILogger<cr9525signupContext> logger)
            : base(options)
        {
             _logger = logger;
        }

        public virtual DbSet<EventSignup> EventSignup { get; set; }
        public virtual DbSet<Timeslot> Timeslot { get; set; }
        public virtual DbSet<UsCities> UsCities { get; set; }
        public virtual DbSet<UsStates> UsStates { get; set; }
        public virtual DbSet<Users> Users { get; set; }

        public virtual DbSet<BanListText> BanListText { get; set; }

        public DbSet<TimeslotLimited> TimeslotLimited { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            //optionsBuilder.UseLoggerFactory(CrLoggerFactory);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasSequence<int>("user_manual_seq")
                .StartsAt(1006)
                .IncrementsBy(1);
            
            modelBuilder.Entity<EventSignup>(entity =>
            {
                entity.ToTable("event_signup");

                entity.Property(e => e.AttendInd).HasColumnName("attend_ind");

                entity.Property(e => e.AttendNbr).HasColumnName("attend_nbr");

                entity.Property(e => e.SignupTms).HasColumnName("signup_tms");

                entity.Property(e => e.TimeslotId).HasColumnName("timeslot_id");

                entity.Property(e => e.UserId).HasColumnName("user_id");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.SignupTxt).HasColumnName("signup_txt");

                entity.Property(e => e.ConfirmInd).HasColumnName("confirm_ind");

                entity.Property(e => e.DeleteInd).HasColumnName("delete_ind");

                entity.Property(e => e.NoShowInd).HasColumnName("noshow_ind");

                entity.Property(e => e.FlexibleInd).HasColumnName("flexible_ind");

                entity.HasOne(d => d.Timeslot)
                    .WithMany()
                    .HasForeignKey(d => d.TimeslotId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_EVENT_TIME");

                entity.HasOne(d => d.User)
                    .WithMany()
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_EVENT_USR");
            });

            modelBuilder.Entity<Timeslot>(entity =>
            {
                entity.ToTable("timeslot");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.EventClosed)
                    .HasColumnName("event_closed")
                    .HasDefaultValueSql("false")
                    .HasComment("Flag to show this event is closed to signups. Set to FALSE to allow signups.");

                entity.Property(e => e.EventEndTms)
                    .HasColumnName("event_end_tms")
                    .HasComment("Date and time the event ends.");

                entity.Property(e => e.EventOpenTms)
                    .HasColumnName("event_open_tms")
                    .HasComment("Date and time the event opens to registrants.");

                entity.Property(e => e.EventSlotCnt)
                    .HasColumnName("event_slot_cnt")
                    .HasDefaultValueSql("10")
                    .HasComment("Number of people permitted at the event.");

                entity.Property(e => e.EventStartTms)
                    .HasColumnName("event_start_tms")
                    .HasComment("Date and time the event starts");

                entity.Property(e => e.OverbookCnt)
                    .HasColumnName("overbook_cnt")
                    .HasDefaultValueSql("5")
                    .HasComment("Overbook factor for the standby list.");

                entity.Property(e => e.SignupCnt)
                    .HasColumnName("signup_cnt")
                    .HasDefaultValueSql("30")
                    .HasComment("Signup limitations.");

                entity.Property(e => e.EventNote)
                    .HasColumnName("event_note")
                    .HasMaxLength(100);

                entity.Property(e => e.PrivateEventInd)
                    .HasColumnName("private_event_ind");

                entity.Property(e => e.IntlEventInd)
                    .HasDefaultValueSql("false")
                    .HasColumnName("intl_event_ind");

            });

            modelBuilder.Entity<UsCities>(entity =>
            {
                entity.ToTable("us_cities");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.City)
                    .IsRequired()
                    .HasColumnName("city")
                    .HasMaxLength(50);

                entity.Property(e => e.IdState).HasColumnName("id_state");

                entity.Property(e => e.StateCd)
                    .HasColumnName("state_cd")
                    .HasMaxLength(3);

                entity.Property(e => e.MetroplexInd)
                    .HasColumnName("metroplex_ind");

                entity.HasOne(d => d.IdStateNavigation)
                    .WithMany(p => p.UsCities)
                    .HasForeignKey(d => d.IdState)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("us_cities_id_state_fkey");
            });

            modelBuilder.Entity<UsStates>(entity =>
            {
                entity.ToTable("us_states");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.StateCode)
                    .HasColumnName("state_code")
                    .HasMaxLength(2)
                    .IsFixedLength();

                entity.Property(e => e.StateName)
                    .HasColumnName("state_name")
                    .HasMaxLength(50);
            });

            modelBuilder.Entity<Users>(entity =>
            {
                entity.ToTable("users");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.AdminFlag)
                    .HasColumnName("admin_flag")
                    .HasDefaultValueSql("false");

                entity.Property(e => e.BanFlag)
                    .HasColumnName("ban_flag")
                    .HasDefaultValueSql("false");

                entity.Property(e => e.CityNm)
                    .HasColumnName("city_nm")
                    .HasMaxLength(100);

                entity.Property(e => e.EventCnt)
                    .HasColumnName("event_cnt")
                    .HasDefaultValueSql("0");

                entity.Property(e => e.FbId)
                    .IsRequired()
                    .HasColumnName("fb_id")
                    .HasMaxLength(30);

                entity.Property(e => e.FirstNm)
                    .IsRequired()
                    .HasColumnName("first_nm")
                    .HasMaxLength(100);

                entity.Property(e => e.LastNm)
                    .IsRequired()
                    .HasColumnName("last_nm")
                    .HasMaxLength(100);

                entity.Property(e => e.RealNm)
                    .HasColumnName("real_nm")
                    .HasMaxLength(200);

                entity.Property(e => e.StateCd)
                    .HasColumnName("state_cd")
                    .HasMaxLength(6);

                entity.Property(e => e.VolunteerFlag)
                    .HasColumnName("volunteer_flag")
                    .HasDefaultValueSql("false");

                entity.Property(e => e.NoShowCnt)
                    .HasColumnName("noshow_cnt");
            });

            modelBuilder.Entity<BanListText>(entity =>
            {
                entity.ToTable("ban_list_text");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.CityNm)
                    .HasColumnName("city_nm")
                    .HasMaxLength(50);

                entity.Property(e => e.FirstNm)
                    .HasColumnName("first_nm")
                    .HasMaxLength(50);

                entity.Property(e => e.LastNm)
                    .HasColumnName("last_nm")
                    .HasMaxLength(50);

                entity.Property(e => e.StateCd)
                    .HasColumnName("state_cd")
                    .HasMaxLength(10);

                entity.Property(e => e.CommentTxt)
                    .HasColumnName("comment_txt")
                    .HasMaxLength(200);
            });

            OnModelCreatingPartial(modelBuilder);
        }


        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
