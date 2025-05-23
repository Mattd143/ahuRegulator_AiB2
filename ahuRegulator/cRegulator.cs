using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ahuKlasy;

namespace ahuRegulator
{

    #region ParametryLokalne

    // przykładowa realizacja - do modyfikacji przez studenta
    class cRegulatorPI
    {
        double Ts = 1;
        public double calka = 0;
        public double kp = 2;
        public double ki = 2;
        public double o1 = 100000; // ograniczenie regulatora (antiwindup)

        public double Wyjscie(double Uchyb)
        {
            double przyrost = Uchyb * Ts;

            if ((calka + przyrost > o1 && Uchyb > 0) ||
                (calka + przyrost < -o1 && Uchyb < 0))
            {
                przyrost = 0;
            }
            else
            {
                calka += przyrost;
            }
            return kp * Uchyb + ki * calka / 60;

        }
    }
    class cRegulatorPI2
    {
        double Ts = 1;
        public double calka = 0;
        public double kp2 = 1;
        public double ki2 = 0.1;
        public double o2 = 100000; // ograniczenie regulatora (antiwindup)

        public double Wyjscie(double Uchyb)
        {
            double przyrost = Uchyb * Ts;
            if ((calka + przyrost > o2 && Uchyb > 0) ||
                    (calka + przyrost < -o2 && Uchyb < 0))
            {
                przyrost = 0;
            }
            else
            {
                calka += przyrost;
            }

            return kp2 * Uchyb + ki2 * calka / 60;
        }
    }


    #endregion


    /// <summary>
    /// przykładowe stany pracy centrali - do zmiany podkądem właściwego projektu
    /// </summary>
    public enum eStanyPracyCentrali
    {
        Stop = 0,
        Praca = 1,
        RozruchWentylatora = 2,
        WychladzanieNagrzewnicy = 3,
        AlarmNagrzewnicy = 4,
        AlarmPresostat = 5,
        AlarmOdzysk = 6
    }



    public class cRegulator
    {
        // ******** tych zmiennych nie ruszamy - są wykorzystywane przez program wywołujący
        public cDaneWeWy DaneWejsciowe = null;
        public cDaneWeWy DaneWyjsciowe = null;
        public double Ts = 1; //czas, co jaki jest wywoływana procedura regulatora
        double czasRampowania_s = 10.0; // czas łagodnego startu w sekundach
        double CzasOdRozpoczeciaRegulacji = 0.0;
        double czasRampowania_s2 = 20;


        // ********* zmienne definiowane przez studenta

        cRegulatorPI RegPI = new cRegulatorPI();
        cRegulatorPI2 RegPI2 = new cRegulatorPI2();


        eStanyPracyCentrali StanPracyCentrali = eStanyPracyCentrali.Stop;


        double CzasOdStartu = 1;   // rekompensata opoznien programu
        double CzasOdStopu = 1;
        double OpoznienieZalaczeniaNagrzewnicy_s = 10;
        double OpoznienieWylaczeniaWentylatora_s = 15;
        double OpoznienieZalaczeniaWentylatora_s = 5;
        double y_reg_1 = 0;
        double y_reg_2 = 0;
        double minReg = -25;
        double maxReg = 10;
        double y_bypass = 0;
        double y_nagrzewnica = 0;
        double przelacznik = 0;
        double alpha = 0;
        double alpha2 = 0;

        public int iWywolanie()
        {
            // wnętrze funkcji dowolnie zmieniane przez studenta

            double t_zad = DaneWejsciowe.Czytaj(eZmienne.TempZadana_C);
            double t_pom = DaneWejsciowe.Czytaj(eZmienne.TempPomieszczenia_C);
            double t_naw = DaneWejsciowe.Czytaj(eZmienne.TempNawiewu_C);
            double t_odz = DaneWejsciowe.Czytaj(eZmienne.TempZaOdzyskiem_C);
            bool boStart = DaneWejsciowe.Czytaj(eZmienne.PracaCentrali) > 0;
            bool termostat = DaneWejsciowe.Czytaj(eZmienne.TermostatPZamrNagrzewnicyWodnej) > 0;
            bool presostat = DaneWejsciowe.Czytaj(eZmienne.PresostatWentylatoraNawiewu) > 0;
            // bool presostat_wyw = DaneWejsciowe.Czytaj(eZmienne.PresostatWentylatoraWywiewu) > 0; Jest zle zdefiniowane w pliku do ktorego nie mamy dostepu ;(

            // algorytm sterowania


            bool boPracaWentylatoraNawiewu = false;
            bool boPracaWentylatoraWywiewu = false;
            bool boPracaPompy = false;


            if (termostat && boStart)
            {
                StanPracyCentrali = eStanyPracyCentrali.AlarmNagrzewnicy;
            }
            else if (boStart && presostat)
            {
                StanPracyCentrali = eStanyPracyCentrali.AlarmPresostat;
            }
            else if (boStart && t_odz <= 2)
            {
                StanPracyCentrali = eStanyPracyCentrali.AlarmOdzysk;
            }

            switch (StanPracyCentrali)
            {
                case eStanyPracyCentrali.Stop:
                    {
                        y_reg_1 = 0;
                        y_reg_2 = 0;
                        RegPI2.calka = 0;
                        RegPI.calka = 0;
                        y_bypass = 0;
                        y_nagrzewnica = 0;
                        CzasOdRozpoczeciaRegulacji = 0;
                        CzasOdStartu = 1;
                        CzasOdStopu = 1;
                        boPracaWentylatoraNawiewu = false;
                        boPracaWentylatoraWywiewu = false;
                        boPracaPompy = false;
                        if (boStart)
                        {
                            StanPracyCentrali = eStanyPracyCentrali.RozruchWentylatora;
                        }
                        break;
                    }
                case eStanyPracyCentrali.RozruchWentylatora:
                    {

                        if (CzasOdStartu < OpoznienieZalaczeniaWentylatora_s)
                        {
                            boPracaWentylatoraNawiewu = false;
                            boPracaWentylatoraWywiewu = false;
                            CzasOdStartu += Ts * 1;
                        }
                        else
                        {
                            boPracaWentylatoraNawiewu = true;
                            boPracaWentylatoraWywiewu = true;

                            StanPracyCentrali = eStanyPracyCentrali.Praca;
                            CzasOdStartu = 1;
                        }

                        break;
                    }
                case eStanyPracyCentrali.Praca:
                    {
                        boPracaWentylatoraNawiewu = true;
                        boPracaWentylatoraWywiewu = true;

                        if (!boStart)
                        {
                            StanPracyCentrali = eStanyPracyCentrali.WychladzanieNagrzewnicy;
                        }

                        CzasOdRozpoczeciaRegulacji += Ts;

                        alpha = Math.Min(1.0, CzasOdRozpoczeciaRegulacji / czasRampowania_s);
                        alpha2 = Math.Min(1.0, CzasOdRozpoczeciaRegulacji / czasRampowania_s2);

                        // Sygnał z pierwszego regulatora PI – bez rampowania
                        y_reg_1 = alpha * RegPI.Wyjscie((t_zad - t_pom));

                        // Drugi regulator działa normalnie
                        y_reg_2 = alpha * RegPI2.Wyjscie((y_reg_1 - t_naw));


                        if (y_reg_2 < minReg) y_reg_2 = minReg;
                        if (y_reg_2 > maxReg) y_reg_2 = maxReg;


                        przelacznik = alpha2 * (y_reg_2 - minReg) / (maxReg - minReg);


                        if (przelacznik <= 0.5)
                        {
                            y_bypass = 200.0 * przelacznik; // narasta liniowo do 100%
                            y_nagrzewnica = 0;
                        }
                        else
                        {
                            y_bypass = 100.0;
                            if (CzasOdStartu < OpoznienieZalaczeniaNagrzewnicy_s)
                            {
                                CzasOdStartu += Ts * 1;
                            }
                            else
                            {
                                // liniowe narastanie od 1% przy s=0.51 do 100% przy s=0.99
                                y_nagrzewnica = ((przelacznik - 0.5) / 0.49) * 99 + 1;
                                if (y_nagrzewnica > 100)
                                    y_nagrzewnica = 100;
                            }
                        }

                        DaneWyjsciowe.Zapisz(eZmienne.Wysterowanie_bypass_pr, y_bypass);
                        DaneWyjsciowe.Zapisz(eZmienne.WysterowanieNagrzewnicy1_pr, y_nagrzewnica);
                        // DaneWyjsciowe.Zapisz(eZmienne.WysterowanieChlodnicy_pr, y_reg_1);   można podglądać działanie regulacji
                        // DaneWyjsciowe.Zapisz(eZmienne.WysterowanieNagrzewnicy2_pr, y_reg_2);
                        // DaneWyjsciowe.Zapisz(eZmienne.WysterowanieRecyrkulacji_pr, alpha);
                        break;
                    }
                case eStanyPracyCentrali.WychladzanieNagrzewnicy:
                    {
                        if (CzasOdStopu < OpoznienieWylaczeniaWentylatora_s)
                        {
                            CzasOdStopu += Ts * 1;
                            y_bypass = 0;
                            boPracaWentylatoraNawiewu = true;
                            boPracaWentylatoraWywiewu = true;
                        }
                        else
                        {
                            StanPracyCentrali = eStanyPracyCentrali.Stop;
                        }

                        break;
                    }
                case eStanyPracyCentrali.AlarmNagrzewnicy:
                    {
                        boPracaPompy = true;
                        boPracaWentylatoraNawiewu = false;
                        boPracaWentylatoraWywiewu = false;
                        DaneWyjsciowe.Zapisz(eZmienne.WysterowanieNagrzewnicy1_pr, 100);
                        if (!termostat)
                        {
                            StanPracyCentrali = eStanyPracyCentrali.Stop;
                        }
                        break;
                    }
                case eStanyPracyCentrali.AlarmPresostat:
                    {
                        boPracaPompy = false;
                        boPracaWentylatoraNawiewu = false;
                        boPracaWentylatoraWywiewu = false;
                        y_bypass = 0;
                        y_nagrzewnica= 0;
                        if (!presostat)
                        {
                            StanPracyCentrali = eStanyPracyCentrali.Stop;
                        }
                        break;
                    }
                case eStanyPracyCentrali.AlarmOdzysk:
                    {
                        boPracaWentylatoraNawiewu = false;
                        boPracaWentylatoraWywiewu = false;
                        DaneWyjsciowe.Zapisz(eZmienne.Wysterowanie_bypass_pr, 100);
                        DaneWyjsciowe.Zapisz(eZmienne.WysterowanieNagrzewnicy1_pr, 0);
                        if (t_odz > 2)
                        {
                            StanPracyCentrali = eStanyPracyCentrali.Stop;
                        }
                        break;
                    }

            }



            // ustawienie wyjść
            if (y_bypass < 100 && StanPracyCentrali != eStanyPracyCentrali.AlarmNagrzewnicy)
            {
                boPracaPompy = false;
                DaneWyjsciowe.Zapisz(eZmienne.Wysterowanie_bypass_pr, y_bypass);
                DaneWyjsciowe.Zapisz(eZmienne.WysterowanieNagrzewnicy1_pr, 0);
            }
            else if (StanPracyCentrali == eStanyPracyCentrali.Praca)
            {
                if (CzasOdStartu >= OpoznienieZalaczeniaNagrzewnicy_s)
                    boPracaPompy = true;
                DaneWyjsciowe.Zapisz(eZmienne.WysterowanieNagrzewnicy1_pr, y_nagrzewnica);
            }
            DaneWyjsciowe.Zapisz(eZmienne.ZezwolenieNaPraceWentylatoraNawiewu, boPracaWentylatoraNawiewu);
            DaneWyjsciowe.Zapisz(eZmienne.ZezwolenieNaPraceWentylatoraWywiewu, boPracaWentylatoraWywiewu);
            DaneWyjsciowe.Zapisz(eZmienne.ZalaczeniePompyNagrzewnicyWodnej1, boPracaPompy);

            return 0;
        }

        public void ZmienParametry()
        {
            // wnętrze funkcji dowolnie zmieniane przez studenta
            fmParametry fm = new fmParametry();

            fm.ki2 = RegPI2.ki2;
            fm.kp2 = RegPI2.kp2;
            fm.kp = RegPI.kp;
            fm.ki = RegPI.ki;
            fm.t1 = OpoznienieZalaczeniaNagrzewnicy_s;
            fm.t2 = OpoznienieWylaczeniaWentylatora_s;
            fm.t3 = OpoznienieZalaczeniaWentylatora_s;
            fm.o1 = RegPI.o1;
            fm.o2 = RegPI2.o2;

            if (fm.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                RegPI.kp = fm.kp;
                RegPI.ki = fm.ki;
                RegPI2.kp2 = fm.kp2;
                RegPI2.ki2 = fm.ki2;
                OpoznienieZalaczeniaNagrzewnicy_s = fm.t1;
                OpoznienieWylaczeniaWentylatora_s = fm.t2;
                OpoznienieZalaczeniaWentylatora_s = fm.t3;
                RegPI.o1 = fm.o1;
                RegPI2.o2 = fm.o2;

            }
        }

    }
}
