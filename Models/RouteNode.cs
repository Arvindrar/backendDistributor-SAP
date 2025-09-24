// In backendDistributor/Models/RouteNode.cs

namespace backendDistributor.Models
{
    public class RouteNode
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public List<RouteNode> Children { get; set; } = new List<RouteNode>();
    }
}