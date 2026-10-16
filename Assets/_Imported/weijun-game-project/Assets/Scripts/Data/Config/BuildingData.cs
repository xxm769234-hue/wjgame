
//功能：存储单个建筑唯一 ID、占地、等级、生产计时、是否可收资源
[System.Serializable]
public class BuildingData
{
    public long buildingId;
    public string buildingType;
    public int level = 1;
    public int posX;
    public int posY;
    //占地格子
    public int occupyW = 1;
    public int occupyH = 1;
    //产出结束时间戳（毫秒）
    public long produceEndTime;
    public bool canCollect;
}
