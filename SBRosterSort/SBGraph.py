from graphviz import Digraph
import json

SBData = open("SBData.json")
FighterDict = json.load(SBData)

graph = Digraph(format='png')

graph.attr("node", shape = "circle")

graph.node("FightersNode", "Fighters")
graph.node("FightsNode", "Fights")

graph.attr("node", shape = "box")

for key, value in FighterDict["Fighters"].items():
    FighterText = key + "\nWin - " + str(value["Wins"]) + "\nLose - " + str(value["Losses"])
    graph.edge("FightersNode", FighterText)

for key, value in FighterDict["SpecificFights"].items():
    graph.edge("FightsNode", key)

graph.view()