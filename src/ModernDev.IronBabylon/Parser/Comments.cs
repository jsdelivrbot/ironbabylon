using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ModernDev.IronBabylon
{
    public partial class Tokenizer
    {
        private void AddComment(Node comment)
        {
            State.TrailingComments.Add(comment);
            State.LeadingComments.Add(comment);
        }

        public void ProcessComment(Node node)
        {
            if (node.Type == "Program" && (node.Body is IList && ((List<Node>) node.Body).Any()))
            {
                return;
            }

            var stack = State.CommentStack;
            Node lastChild = null;
            var trailingComments = new List<Node>();
            int i;

            if (State.TrailingComments.Any())
            {
                if (State.TrailingComments[0].Start >= node.End)
                {
                    trailingComments = State.TrailingComments.ToList();
                    State.TrailingComments = new List<Node>();
                }
                else
                {
                    State.TrailingComments.Clear();
                }
            }
            else
            {
                if (stack.Any())
                {
                    var lastInStack = stack.Last();

                    if (lastInStack.TrailingComments?.Count > 0 &&
                        lastInStack.TrailingComments[0].Start >= node.End)
                    {
                        trailingComments = lastInStack.TrailingComments.ToList();
                        lastInStack.TrailingComments = null;
                    }
                }
            }

            while (stack.Any() && stack.Last().Start >= node.Start)
            {
                lastChild = stack.Pop();
            }

            if (lastChild != null)
            {
                if (lastChild.LeadingComments?.Count > 0)
                {
                    if (lastChild != node && lastChild.LeadingComments.Last().End <= node.Start)
                    {
                        node.LeadingComments = lastChild.LeadingComments.ToList();
                        lastChild.LeadingComments = null;
                    }
                    else
                    {
                        for (i = lastChild.LeadingComments.Count - 2; i >= 0; --i)
                        {
                            if (lastChild.LeadingComments[i].End <= node.Start)
                            {
                                node.LeadingComments = lastChild.LeadingComments.Splice(0, i + 1).ToList();

                                break;
                            }
                        }
                    }
                }
            }
            else if (State.LeadingComments.Any())
            {
                if (State.LeadingComments.Last().End <= node.Start)
                {
                    node.LeadingComments = State.LeadingComments.ToList();
                    State.LeadingComments = new List<Node>();
                }
                else
                {
                    for (i = 0; i < State.LeadingComments.Count; i++)
                    {
                        if (State.LeadingComments[i].End > node.Start)
                        {
                            break;
                        }
                    }

                    node.LeadingComments = State.LeadingComments.Slice(0, i).ToList();

                    if (node.LeadingComments.Count == 0)
                    {
                        node.LeadingComments = null;
                    }

                    trailingComments = State.LeadingComments.Slice(i).ToList();

                    if (trailingComments.Count == 0)
                    {
                        trailingComments = null;
                    }
                }
            }

            if (trailingComments != null)
            {
                if (trailingComments.Any() && trailingComments[0].Start >= node.Start &&
                    trailingComments.Last().End <= node.End)
                {
                    node.InnerComments = trailingComments.ToList();
                }
                else
                {
                    node.TrailingComments = trailingComments.ToList();
                }
            }

            stack.Add(node);
        }
    }
}
