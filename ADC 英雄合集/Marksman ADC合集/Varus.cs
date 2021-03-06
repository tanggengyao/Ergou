#region
using System;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using Color = System.Drawing.Color;
#endregion

namespace Marksman
{
    internal class Varus : Champion
    {
        public static Spell Q, W, E, R;
        private float LastSpellTick;

        public Varus()
        {
            Utils.PrintMessage("Varus loaded!");

            Q = new Spell(SpellSlot.Q, 1600f);
            W = new Spell(SpellSlot.W);
            E = new Spell(SpellSlot.E, 925f);
            R = new Spell(SpellSlot.R, 1200f);

            Q.SetSkillshot(.25f, 70f, 1650f, false, SkillshotType.SkillshotLine);
            E.SetSkillshot(.50f, 250f, 1400f, false, SkillshotType.SkillshotCircle);
            R.SetSkillshot(.25f, 100f, 1950f, false, SkillshotType.SkillshotLine);

            Q.SetCharged("VarusQ", "VarusQ", 250, 1600, 1.2f);

            Obj_AI_Base.OnProcessSpellCast += Obj_AI_Hero_OnProcessSpellCast;
        }

        static Varus()
        {
            QMinCharge = 0;
        }

        public static float QMinCharge { get; set; }

        private void Obj_AI_Hero_OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (!sender.IsMe || args.SData.Name.ToLower().Contains("attack"))
                return;

            LastSpellTick = Environment.TickCount;
        }

        public static Obj_AI_Hero EnemyWStackCount(int buffCount)
        {
            return
                (from enemy in
                    ObjectManager.Get<Obj_AI_Hero>()
                        .Where(
                            xEnemy =>
                                xEnemy.IsEnemy && !xEnemy.IsDead && ObjectManager.Player.Distance(xEnemy) < Q.Range &&
                                W.Level > 0)
                    from buff in enemy.Buffs
                    where buff.Name == "varuswdebuff" && buff.Count >= buffCount
                    select enemy).FirstOrDefault();
        }


        public override void Drawing_OnDraw(EventArgs args)
        {
            var drawQ = GetValue<Circle>("DrawQ");
            var drawE = GetValue<Circle>("DrawE");
            var drawR = GetValue<Circle>("DrawR");
            var drawQC = GetValue<Circle>("DrawQC");
            var drawRS = GetValue<Circle>("DrawRS");

            if (drawQ.Active)
                Utility.DrawCircle(ObjectManager.Player.Position, Q.Range, drawQ.Color, 1, 5);

            if (drawE.Active)
                Utility.DrawCircle(ObjectManager.Player.Position, E.Range, drawE.Color, 1, 5);

            if (drawQC.Active)
                Utility.DrawCircle(ObjectManager.Player.Position, GetValue<Slider>("UseQMinChargeC").Value, drawQC.Color, 1, 5);

            if (drawR.Active)
                Utility.DrawCircle(ObjectManager.Player.Position, R.Range, drawR.Color, 1, 5);
            
            if (GetValue<KeyBind>("CastR").Active && drawRS.Active)
            {
                Vector3 DrawPosition;

                if (ObjectManager.Player.Distance(Game.CursorPos) < R.Range - 300f)
                    DrawPosition = Game.CursorPos;
                else
                    DrawPosition = ObjectManager.Player.Position +
                                   Vector3.Normalize(Game.CursorPos - ObjectManager.Player.Position)*(R.Range - 300f);

                Utility.DrawCircle(DrawPosition, 300f, drawRS.Color, 1, 5);
            }
        }

        private static float CalcWExplodeDamage(Obj_AI_Hero vTarget)
        {
            var wExplodePerStack = new[] {2f, 2.75f, 3.5f, 4.25f, 5f};
            float xWDamage = /*EnemyWStackCount **/ wExplodePerStack[W.Level - 1];

            float fxWDamage = vTarget.Health/100*xWDamage;
            return fxWDamage;
        }

        public static float CalcQDamage(Obj_AI_Hero vTarget)
        {
            if (Q.Level == 0)
                return 0;



            var xQMin = Program.CClass.GetValue<Slider>("UseQMinChargeC").Value;
            var qMinRange = xQMin < Orbwalking.GetRealAutoAttackRange(vTarget)
                ? Orbwalking.GetRealAutoAttackRange(vTarget)
                : xQMin;
            var qMaxRange = Q.ChargedMaxRange;
            
            var qCalcRange = qMaxRange/qMinRange;

            var qDamageMinPerLevel = new[] {10f, 47f, 83f, 120f, 157f};
            var qDamageMaxPerLevel = new[] {15f, 70f, 125f, 180f, 235f};


            //Game.PrintChat(qDamageMinPerLevel[Q.Level - 1].ToString() + " : " + qDamageMaxPerLevel[Q.Level - 1].ToString());
            double fxQDamage = qDamageMinPerLevel[Q.Level - 1] + qDamageMaxPerLevel[Q.Level - 1]* 1.6; 
            fxQDamage = fxQDamage/ qCalcRange;

            return (float) fxQDamage;
        }

        private static void CastQEnemy(Obj_AI_Hero vTarget)
        {
            if (vTarget == null)
                return;
            if (!Q.IsReady())
                return;

            if (Q.IsCharging)
            {
                ObjectManager.Player.IssueOrder(GameObjectOrder.MoveTo, Game.CursorPos);
                if (Q.Range >= QMinCharge)
                {
                    Q.Cast(vTarget, false, true);
                }
            }
            else
            {
                Q.StartCharging();
            }
        }

        public override void Game_OnGameUpdate(EventArgs args)
        {
            
            if (GetValue<KeyBind>("CastR").Active)
            {
                Vector3 searchPos;

                if (ObjectManager.Player.Distance(Game.CursorPos) < R.Range - 300f)
                    searchPos = Game.CursorPos;
                else
                    searchPos = ObjectManager.Player.Position +
                                Vector3.Normalize(Game.CursorPos - ObjectManager.Player.Position)*(R.Range - 300f);

                var rTarget =
                    ObjectManager.Get<Obj_AI_Hero>()
                        .Where(hero => hero.IsValidTarget(R.Range) && hero.Distance(searchPos) < 300f)
                        .OrderByDescending(SimpleTs.GetPriority)
                        .First();

                if (rTarget != null && R.IsReady())
                    R.Cast(rTarget);
            }

            if (E.IsReady() && GetValue<KeyBind>("UseETH").Active)
            {
                if(ObjectManager.Player.HasBuff("Recall"))
                    return;
                var t = SimpleTs.GetTarget(E.Range, SimpleTs.DamageType.Physical);
                if (t != null)
                    E.Cast(t, false, true);
            }           

            if (!ComboActive && !HarassActive) return;

            var useQ = GetValue<StringList>("UseQ" + (ComboActive ? "C" : "H"));
            var useW = GetValue<Slider>("UseW" + (ComboActive ? "C" : "H"));
            var useE = GetValue<bool>("UseE" + (ComboActive ? "C" : "H"));

            QMinCharge = GetValue<Slider>("UseQMinChargeC").Value;

            var qTarget = SimpleTs.GetTarget(Q.ChargedMaxRange, SimpleTs.DamageType.Physical);
            var eTarget = SimpleTs.GetTarget(E.Range, SimpleTs.DamageType.Physical);

            //if (qTarget != null)
            //    Game.PrintChat("Q Damage: " + CalcQDamage(qTarget));

            if (qTarget.Health < CalcQDamage(qTarget))
                CastQEnemy(qTarget);

            if (useE && E.IsReady() && eTarget != null)
                E.Cast(eTarget, false, true);


            if (ObjectManager.Player.Distance(qTarget) > QMinCharge ||
                ObjectManager.Player.Distance(qTarget) > Orbwalking.GetRealAutoAttackRange(qTarget)) 
                CastQEnemy(qTarget);
            else
            {
                switch (useQ.SelectedIndex)
                {
                    case 2:
                        {
                            CastQEnemy(EnemyWStackCount(useW.Value));
                            break;
                        }

                    case 3:
                        {
                            CastQEnemy(EnemyWStackCount(3));
                            break;
                        }
                }
            }

            switch (useQ.SelectedIndex)
            {
                case 1:
                {
                    CastQEnemy(qTarget);
                    break;
                }
                case 2:
                {
                    CastQEnemy(EnemyWStackCount(useW.Value));
                    break;
                }

                case 3:
                {
                    CastQEnemy(EnemyWStackCount(3));
                    break;
                }
            }

        }

        public override void Orbwalking_BeforeAttack(Orbwalking.BeforeAttackEventArgs args)
        {
            args.Process = !Q.IsCharging;
        }

        public override bool ComboMenu(Menu config)
        {
            config.AddItem(
                new MenuItem("UseQC" + Id, "Q施放：").SetValue(
                    new StringList(new[] {"禁用", "任何时间", "W枯萎效果", "3层W枯萎效果"}, 3)));
            config.AddItem(
                new MenuItem("UseQMinChargeC" + Id, "最小 Q 蓄力范围").SetValue(new Slider(1000, 250, 1400)));
            config.AddItem(new MenuItem("UseWC" + Id, "使用 W").SetValue(new Slider(3, 1, 3)));
            config.AddItem(new MenuItem("UseEC" + Id, "使用 E").SetValue(true));

            return true;
        }

        public override bool HarassMenu(Menu config)
        {
            config.AddItem(
                new MenuItem("UseQH" + Id, "Q施放：").SetValue(
                    new StringList(new[] {"禁用", "任何时间", "W枯萎效果", "3层W枯萎效果"}, 3)));
            config.AddItem(new MenuItem("UseWH" + Id, "使用 W").SetValue(new Slider(3, 1, 3)));
            config.AddItem(new MenuItem("UseEH" + Id, "使用 E").SetValue(true));
            config.AddItem(
                new MenuItem("UseETH" + Id, "使用 E (自动)").SetValue(new KeyBind("H".ToCharArray()[0],
                    KeyBindType.Toggle)));             

            return true;
        }

        public override bool MiscMenu(Menu config)
        {
            config.AddItem(new MenuItem("spellDelay" + Id, "法术延迟").SetValue(new Slider(500, 0, 3000)));
            config.AddItem(
                new MenuItem("CastR" + Id, "手动 R").SetValue(new KeyBind("T".ToCharArray()[0], KeyBindType.Press)));

            return true;
        }

        public override bool DrawingMenu(Menu config)
        {
            config.AddItem(new MenuItem("DrawQ" + Id, "Q 范围").SetValue(new Circle(true, Color.DarkGray)));
            config.AddItem(new MenuItem("DrawE" + Id, "E 范围").SetValue(new Circle(true, Color.DarkGray)));
            config.AddItem(new MenuItem("DrawR" + Id, "R 范围").SetValue(new Circle(true, Color.DarkGray)));
            config.AddItem(new MenuItem("DrawQC" + Id, "最小 Q 蓄力范围").SetValue(new Circle(true, Color.White)));
            config.AddItem(new MenuItem("DrawRS" + Id, "R: 束缚 面积").SetValue(new Circle(true, Color.White)));

            return true;
        }

        public override bool ExtrasMenu(Menu config)
        {
            return true;
        }

        public override bool LaneClearMenu(Menu config)
        {
            return true;
        }

    }
}
