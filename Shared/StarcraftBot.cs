using BWAPI.NET;


namespace Shared;

// library from https://www.nuget.org/packages/BWAPI.NET

public class MyStarcraftBot : DefaultBWListener
{
  private BWClient _bwClient;
  private Game _game;
    
  public void StartBot()
  {
    _bwClient = new BWClient(this);
  }

  public override void OnStart()
  {
    Game = _bwClient?.Game;
  }

  public override void OnEnd(bool isWinner)
  {
  }

  public override void OnFrame()
  {
    if (Game == null)
      return;
      _game.DrawTextScreen(100, 100, "Hello Bot!");
  }

  public override void OnUnitComplete(Unit unit)
  {
  }

  public override void OnUnitDestroy(Unit unit)
  {
  }

  public override void OnUnitMorph(Unit unit)
  {
  }

  public override void OnSendText(string text)
  {
  }

  public override void OnReceiveText(Player player, string text)
  {
  }

  public override void OnPlayerLeft(Player player)
  {
  }

  public override void OnNukeDetect(Position target)
  {
  }

  public override void OnUnitEvade(Unit unit)
  {
  }

  public override void OnUnitShow(Unit unit)
  {
  }

  public override void OnUnitHide(Unit unit)
  {
  }

  public override void OnUnitCreate(Unit unit)
  {
  }

  public override void OnUnitRenegade(Unit unit)
  {
  }

  public override void OnSaveGame(string gameName)
  {
  }

  public override void OnUnitDiscover(Unit unit)
  {
  }
}